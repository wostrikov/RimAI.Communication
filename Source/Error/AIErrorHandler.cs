using System;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Client.ProviderPolicy;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Threading;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Error;

public static class AIErrorHandler
{
    private static bool _quotaWarningShown;
    // A provider that is down fails every request in turn; one notice per half minute is enough.
    private static readonly FailureNoticeGate GenerationFailures = new(TimeSpan.FromSeconds(30));

    public static async Task<CommunicationProviderOutcome> ExecuteLogicalRequest(
        Func<CommunicationProviderSlot, Task<CommunicationProviderAttempt>> attempt,
        CancellationToken cancellationToken,
        string requestId = null)
    {
        var settings = Settings.Get();
        var chain = CommunicationProviderChain.Build(settings?.ToProviderChainRequest());
        var outcome = await CommunicationProviderOrchestrator.ExecuteAsync(
            chain,
            async slot =>
            {
                try
                {
                    return await attempt(slot);
                }
                catch (OperationCanceledException)
                {
                    return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Cancelled);
                }
            },
            requestId,
            cancellationToken);

        LogOutcome(outcome);
        if (!outcome.Succeeded && outcome.TerminalClass != CommunicationFailureClass.Cancelled)
            HandleFinalFailure(ToException(outcome));
        return outcome;
    }

    public static CommunicationProviderAttempt ToAttempt(
        object value,
        int deliveredTalkObjects,
        string response = null,
        string errorMessage = null,
        bool expectTalkObjects = true)
    {
        var payload = value as Payload;
        var text = response ?? payload?.Response;
        var error = errorMessage ?? payload?.ErrorMessage;
        if (!expectTalkObjects)
        {
            if (!string.IsNullOrWhiteSpace(error))
                return CommunicationProviderAttempt.Fail(CommunicationFailureClassifier.FromPayload(error, text, 0), value, text);
            if (string.IsNullOrWhiteSpace(text))
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.EmptyResponse, value, text);
            return CommunicationProviderAttempt.Success(value, text);
        }

        var failure = CommunicationFailureClassifier.FromPayload(error, text, deliveredTalkObjects);
        if (failure == CommunicationFailureClass.None)
            return CommunicationProviderAttempt.Success(value, text);
        return CommunicationProviderAttempt.Fail(failure, value, text);
    }

    static void LogOutcome(CommunicationProviderOutcome outcome)
    {
        if (outcome == null)
            return;
        foreach (var attempt in outcome.Attempts)
        {
            if (attempt.Kind == CommunicationAttemptKind.Success && outcome.Attempts.Count == 1)
                continue;
            Logger.Warning(
                $"[RIMAI_TEXT_AI] request={outcome.RequestId} attempt={attempt.AttemptNumber} provider={attempt.Provider} class={attempt.FailureClass} kind={attempt.Kind} terminal={outcome.TerminalClass} success={outcome.SuccessfulProvider}");
        }
    }

    static Exception ToException(CommunicationProviderOutcome outcome)
    {
        if (outcome?.TerminalClass == CommunicationFailureClass.RateLimited)
            return new QuotaExceededException(outcome.TerminalClass.ToString(), outcome.Value as Payload);
        if (outcome?.TerminalClass == CommunicationFailureClass.Cancelled)
            return new OperationCanceledException(outcome.TerminalClass.ToString());
        if (outcome?.Value is Payload payload)
            return new AIRequestException(outcome.TerminalClass.ToString(), payload);
        return new AIRequestException(outcome?.TerminalClass.ToString() ?? "Unknown", null);
    }

    public static void ResetQuotaWarning()
    {
        _quotaWarningShown = false;
    }

    static void HandleFinalFailure(Exception ex)
    {
        if (ex is QuotaExceededException)
        {
            ShowQuotaWarning(ex);
        }
        else if (ex is OperationCanceledException)
        {
            Logger.Warning(ex.Message);
        }
        else
        {
            ShowGenerationWarning(ex);
        }
    }

    static void ShowQuotaWarning(Exception ex)
    {
        if (!_quotaWarningShown)
        {
            _quotaWarningShown = true;
            Logger.Warning(ex.Message);
            MainThreadSchedulerAccess.RunInlineOrEnqueue(() =>
                Messages.Message("RimTalk.TalkService.QuotaExceeded".Translate(), MessageTypeDefOf.NeutralEvent, false));
        }
    }

    static void ShowGenerationWarning(Exception ex)
    {
        Logger.Warning(ex.Message);
        if (!GenerationFailures.Record(DateTime.UtcNow))
            return;

        string reason = ex.Message;
        // Messages belong to the main thread; a failure is reported from the request's own.
        MainThreadSchedulerAccess.RunInlineOrEnqueue(() =>
        {
            int failures = GenerationFailures.TakeForNotice(DateTime.UtcNow);
            string message = $"{"RimTalk.TalkService.GenerationFailed".Translate()}: {reason}";
            if (failures > 1)
                message += " " + "RimTalk.TalkService.GenerationFailedCount".Translate(failures);
            Messages.Message(message, MessageTypeDefOf.NeutralEvent, false);
        });
    }
}
