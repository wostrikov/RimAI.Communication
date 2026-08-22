using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ustas.RimAI.Communication.Client.ProviderPolicy;

public enum CommunicationAttemptKind
{
    Success = 0,
    SameProviderRetry = 1,
    ProviderFailover = 2,
    Terminal = 3
}

public sealed class CommunicationProviderAttempt
{
    public bool Succeeded { get; set; }
    public CommunicationFailureClass FailureClass { get; set; }
    public string Text { get; set; }
    public object Value { get; set; }

    public static CommunicationProviderAttempt Success(object value, string text = null) =>
        new CommunicationProviderAttempt
        {
            Succeeded = true,
            FailureClass = CommunicationFailureClass.None,
            Value = value,
            Text = text
        };

    public static CommunicationProviderAttempt Fail(CommunicationFailureClass failureClass, object value = null, string text = null) =>
        new CommunicationProviderAttempt
        {
            Succeeded = false,
            FailureClass = failureClass,
            Value = value,
            Text = text
        };
}

public sealed class CommunicationProviderAttemptRecord
{
    public int AttemptNumber { get; set; }
    public string SlotId { get; set; }
    public string Provider { get; set; }
    public CommunicationFailureClass FailureClass { get; set; }
    public CommunicationAttemptKind Kind { get; set; }
}

public sealed class CommunicationProviderOutcome
{
    public string RequestId { get; set; }
    public bool Succeeded { get; set; }
    public CommunicationFailureClass TerminalClass { get; set; }
    public string SuccessfulSlotId { get; set; }
    public string SuccessfulProvider { get; set; }
    public object Value { get; set; }
    public IList<CommunicationProviderAttemptRecord> Attempts { get; } = new List<CommunicationProviderAttemptRecord>();
    public bool ProvidersExhausted { get; set; }
}

/// <summary>
/// Authoritative Communication failover loop. One logical request, ordered
/// provider slots, no duplicate attempts, cancellation stops the chain.
/// Same-provider retry is not performed here.
/// </summary>
public static class CommunicationProviderOrchestrator
{
    public static async Task<CommunicationProviderOutcome> ExecuteAsync(
        IReadOnlyList<CommunicationProviderSlot> chain,
        Func<CommunicationProviderSlot, Task<CommunicationProviderAttempt>> attempt,
        string requestId,
        CancellationToken cancellationToken)
    {
        if (attempt == null)
            throw new ArgumentNullException(nameof(attempt));

        var outcome = new CommunicationProviderOutcome
        {
            RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId
        };

        if (chain == null || chain.Count == 0)
        {
            outcome.TerminalClass = CommunicationFailureClass.Configuration;
            outcome.ProvidersExhausted = true;
            outcome.Attempts.Add(new CommunicationProviderAttemptRecord
            {
                AttemptNumber = 0,
                SlotId = "none",
                Provider = "none",
                FailureClass = CommunicationFailureClass.Configuration,
                Kind = CommunicationAttemptKind.Terminal
            });
            return outcome;
        }

        for (var i = 0; i < chain.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Record(outcome, i + 1, chain[i], CommunicationFailureClass.Cancelled, CommunicationAttemptKind.Terminal);
                outcome.TerminalClass = CommunicationFailureClass.Cancelled;
                return outcome;
            }

            var slot = chain[i];
            CommunicationProviderAttempt result;
            try
            {
                result = await attempt(slot) ?? CommunicationProviderAttempt.Fail(CommunicationFailureClass.Unknown);
            }
            catch (OperationCanceledException)
            {
                Record(outcome, i + 1, slot, CommunicationFailureClass.Cancelled, CommunicationAttemptKind.Terminal);
                outcome.TerminalClass = CommunicationFailureClass.Cancelled;
                return outcome;
            }

            if (result.Succeeded && result.FailureClass == CommunicationFailureClass.None)
            {
                Record(outcome, i + 1, slot, CommunicationFailureClass.None, CommunicationAttemptKind.Success);
                outcome.Succeeded = true;
                outcome.SuccessfulSlotId = slot.SlotId;
                outcome.SuccessfulProvider = slot.DiagnosticName;
                outcome.Value = result.Value;
                return outcome;
            }

            var failure = result.FailureClass == CommunicationFailureClass.None
                ? CommunicationFailureClass.Unknown
                : result.FailureClass;
            var disposition = CommunicationFailurePolicy.For(failure);
            var isLast = i == chain.Count - 1;
            var stop = disposition.Terminal || !disposition.Failover || isLast;
            Record(
                outcome,
                i + 1,
                slot,
                failure,
                stop ? CommunicationAttemptKind.Terminal : CommunicationAttemptKind.ProviderFailover);
            outcome.Value = result.Value;
            if (stop)
            {
                outcome.TerminalClass = failure;
                outcome.ProvidersExhausted = isLast && !disposition.Terminal;
                return outcome;
            }
        }

        outcome.TerminalClass = CommunicationFailureClass.ProviderUnavailable;
        outcome.ProvidersExhausted = true;
        return outcome;
    }

    static void Record(
        CommunicationProviderOutcome outcome,
        int attemptNumber,
        CommunicationProviderSlot slot,
        CommunicationFailureClass failureClass,
        CommunicationAttemptKind kind)
    {
        outcome.Attempts.Add(new CommunicationProviderAttemptRecord
        {
            AttemptNumber = attemptNumber,
            SlotId = slot?.SlotId,
            Provider = slot?.DiagnosticName,
            FailureClass = failureClass,
            Kind = kind
        });
    }
}
