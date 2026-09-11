using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Client;
using Ustas.RimAI.Communication.Client.ProviderPolicy;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Error;
using Ustas.RimAI.Communication.Util;

namespace Ustas.RimAI.Communication.Service;

// WARNING:
// This class defines core logic and has a significant impact on system behavior.
// In most cases, you should NOT modify this file.
public static class AIService
{
    // volatile: set on the main thread, cleared in a finally on a threadpool thread and read on
    // the main thread every tick. A cached stale `true` would silence the whole colony for good.
    private static volatile bool _busy;
    private static DateTime? _busySince;
    private static bool _firstInstruction = true;

    /// <summary>
    /// Streaming chat that invokes callback as each player's dialogue is parsed
    /// </summary>
    public static async Task ChatStreaming(TalkRequest request, Action<TalkResponse> onPlayerResponseReceived)
    {
        var prefixMessages = request.PromptMessages ?? [];
        var apiLog = ApiHistory.AddRequest(request, Channel.Stream);
        var lastApiLog = apiLog;

        var delivered = 0;
        var payload = await ExecuteWithRetry(apiLog, async client =>
        {
            // All prompt messages are already in prefixMessages, pass empty list for messages
            return await client.GetStreamingChatCompletionAsync<TalkResponse>(prefixMessages, [],
                response =>
                {
                    if (Cache.GetByName(response.Name) == null) return;
                    delivered++;
                    response.TalkType = request.TalkType;

                    // Calculate timing relative to the correct previous log
                    int elapsedMs = (int)(DateTime.Now - lastApiLog.Timestamp).TotalMilliseconds;
                    if (lastApiLog == apiLog) elapsedMs -= lastApiLog.ElapsedMs;

                    var newLog = ApiHistory.AddResponse(apiLog.Id, response.Text, response.Name,
                        response.InteractionRaw, elapsedMs: elapsedMs);
                    
                    response.Id = newLog.Id;
                    lastApiLog = newLog;

                    onPlayerResponseReceived?.Invoke(response);
                },
                prep => ApiHistory.UpdatePayload(apiLog.Id, prep));
        }, () => delivered, expectTalkObjects: true);

        HandleFinalStatus(apiLog, payload);
        _firstInstruction = false;
    }

    // One time query - used for generating persona, etc
    public static async Task<T> Query<T>(TalkRequest request) where T : class, IJsonData
    {
        var messages = new List<(Role role, string message)> { (Role.User, request.Prompt) };
        var prefixMessages = new List<(Role role, string message)> { (Role.System, request.Context) };
        var apiLog = ApiHistory.AddRequest(request, Channel.Query);

        var payload = await ExecuteWithRetry(
            apiLog,
            async client =>
                await client.GetChatCompletionAsync(prefixMessages, messages, prep => ApiHistory.UpdatePayload(apiLog.Id, prep)),
            expectTalkObjects: false);

        if (string.IsNullOrEmpty(payload.Response) || !string.IsNullOrEmpty(payload.ErrorMessage))
        {
            ApiHistory.UpdatePayload(apiLog.Id, payload);
            return null;
        }

        try
        {
            var data = JsonUtil.DeserializeFromJson<T>(payload.Response);
            ApiHistory.AddResponse(apiLog.Id, data.GetText(), null, null, payload: payload);
            return data;
        }
        catch (Exception)
        {
            ReportError(apiLog, payload, "Json Deserialization Failed");
            return null;
        }
    }

    private static async Task<Payload> ExecuteWithRetry(
        ApiLog apiLog,
        Func<IAIClient, Task<Payload>> action,
        Func<int> deliveredTalkObjects = null,
        bool expectTalkObjects = true)
    {
        _busy = true;
        _busySince = DateTime.Now;
        try
        {
            var requestId = apiLog?.Id.ToString("N");
            var outcome = await AIErrorHandler.ExecuteLogicalRequest(
                async slot =>
                {
                    var client = await AIClientFactory.CreateClientAsync(slot);
                    if (client == null)
                        return CommunicationProviderAttempt.Fail(CommunicationFailureClass.ProviderUnavailable);
                    try
                    {
                        var payload = await action(client);
                        if (payload == null)
                            return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Cancelled);
                        return AIErrorHandler.ToAttempt(
                            payload,
                            deliveredTalkObjects?.Invoke() ?? 0,
                            expectTalkObjects: expectTalkObjects);
                    }
                    catch (OperationCanceledException)
                    {
                        return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Cancelled);
                    }
                    catch (TimeoutException ex)
                    {
                        return DeliveredOrFail(deliveredTalkObjects, CommunicationFailureClass.Timeout, ex.Message);
                    }
                    catch (QuotaExceededException ex)
                    {
                        return DeliveredOrFail(deliveredTalkObjects, CommunicationFailureClass.RateLimited, ex.Message, ex.Payload);
                    }
                    catch (AIRequestException ex)
                    {
                        return DeliveredOrFail(
                            deliveredTalkObjects,
                            CommunicationFailureClassifier.FromException(ex),
                            ex.Message,
                            ex.Payload);
                    }
                },
                default,
                requestId);

            if (outcome.Succeeded)
            {
                var payload = outcome.Value as Payload ?? new Payload("Unknown", outcome.SuccessfulProvider, "", "", 0);
                Stats.IncrementCalls();
                Stats.IncrementTokens(payload.TokenCount);
                return payload;
            }

            if (apiLog != null)
            {
                apiLog.Response = outcome.TerminalClass.ToString();
                apiLog.IsError = true;
            }

            return outcome.Value as Payload
                ?? new Payload("Unknown", "Unknown", "", null, 0, outcome.TerminalClass.ToString());
        }
        finally
        {
            _busy = false;
            _busySince = null;
        }
    }

    static CommunicationProviderAttempt DeliveredOrFail(
        Func<int> deliveredTalkObjects,
        CommunicationFailureClass failureClass,
        string message,
        Payload payload = null)
    {
        if ((deliveredTalkObjects?.Invoke() ?? 0) > 0)
            return CommunicationProviderAttempt.Success(payload, payload?.Response);
        return CommunicationProviderAttempt.Fail(failureClass, payload, message);
    }

    private static void HandleFinalStatus(ApiLog apiLog, Payload payload)
    {
        // If response is empty but no explicit error yet, mark as deserialization failure (or empty response)
        if (string.IsNullOrEmpty(apiLog.Response) && !apiLog.IsError && string.IsNullOrEmpty(payload.ErrorMessage))
        {
            ReportError(apiLog, payload, "Json Deserialization Failed");
            return;
        }
        
        ApiHistory.UpdatePayload(apiLog.Id, payload);
    }

    private static void ReportError(ApiLog apiLog, Payload payload, string errorMsg)
    {
        apiLog.Response = $"{errorMsg}\n\nRaw Response:\n{payload.Response}";
        apiLog.IsError = true;
        payload.ErrorMessage = errorMsg;
        ApiHistory.UpdatePayload(apiLog.Id, payload);
    }

    public static bool IsFirstInstruction() => _firstInstruction;
    public static bool IsBusy()
    {
        if (!BusyGate.IsStuck(_busy, _busySince, DateTime.Now)) return _busy;

        Logger.Warning($"The AI slot has been held for over {BusyGate.StuckAfterSeconds}s. Releasing it - " +
                       "no request can legitimately take that long, and while it is held nobody in the colony can speak.");
        _busy = false;
        _busySince = null;
        return false;
    }

    public static void Clear()
    {
        _busy = false;
        _busySince = null;
        _firstInstruction = true;
    }
}