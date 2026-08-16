using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Error;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Net;
using Ustas.RimAI.Core.Player2;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Client.Player2;

public class Player2Client : IAIClient
{
    private readonly string _apiKey;
    private readonly bool _isLocalConnection;
    private readonly string _baseUrl;

    private string CurrentApiUrl => _baseUrl;

    private Player2Client(Player2EnsureResult session)
    {
        _apiKey = session.ApiKey;
        _isLocalConnection = session.IsLocal;
        _baseUrl = session.BaseUrl;
    }

    public static async Task<Player2Client> CreateAsync(string fallbackApiKey = null)
    {
        try
        {
            var session = await Player2Session.Current.EnsureAuthenticatedAsync(
                new Player2AuthRequest { FallbackApiKey = fallbackApiKey });
            if (session.Succeeded)
            {
                if (session.IsLocal)
                {
                    Logger.Debug("Player2 local app detected.");
                    ShowNotification("RimTalk.Player2.LocalDetected", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Logger.Debug("Using manual Player2 API key.");
                }

                return new Player2Client(session);
            }

            ShowNotification("RimTalk.Player2.LocalNotFound", MessageTypeDefOf.CautionInput);
            throw new Exception(session.Error ?? "Player2 not available: no local app and no API key.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create Player2 client: {ex.Message}");
            throw;
        }
    }

    public async Task<Payload> GetChatCompletionAsync(List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages,
        Action<Payload> onRequestPrepared = null)
    {
        string jsonContent = BuildRequestJson(prefixMessages, messages, stream: false);
        onRequestPrepared?.Invoke(new Payload(CurrentApiUrl, null, jsonContent, null, 0));
        string responseText = await SendRequestAsync($"{CurrentApiUrl}/v1/chat/completions", jsonContent);

        var response = JsonUtil.DeserializeFromJson<Player2Response>(responseText);
        var content = response?.Choices?[0]?.Message?.Content;
        var tokens = response?.Usage?.TotalTokens ?? 0;

        return new Payload(CurrentApiUrl, null, jsonContent, content, tokens);
    }

    public async Task<Payload> GetStreamingChatCompletionAsync<T>(List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages,
        Action<T> onResponseParsed,
        Action<Payload> onRequestPrepared = null) where T : class
    {
        string jsonContent = BuildRequestJson(prefixMessages, messages, stream: true);
        onRequestPrepared?.Invoke(new Payload(CurrentApiUrl, null, jsonContent, null, 0));
        var jsonParser = new JsonStreamParser<T>();

        var streamHandler = new Player2StreamHandler(chunk =>
        {
            foreach (var item in jsonParser.Parse(chunk))
                onResponseParsed?.Invoke(item);
        });

        await SendRequestAsync($"{CurrentApiUrl}/v1/chat/completions", jsonContent, streamHandler);

        return new Payload(CurrentApiUrl, null, jsonContent, streamHandler.GetFullText(),
            streamHandler.GetTotalTokens());
    }

    private string BuildRequestJson(List<(Role role, string message)> prefixMessages, List<(Role role, string message)> messages, bool stream)
    {
        var rawMessages = new List<(Role role, string message)>();
        if (prefixMessages != null) rawMessages.AddRange(prefixMessages);
        if (messages != null) rawMessages.AddRange(messages);

        var mergedMessages = new List<Message>();
        foreach (var m in rawMessages)
        {
            var roleStr = RoleToString(m.role);
            if (mergedMessages.Count > 0 && mergedMessages.Last().Role == roleStr)
            {
                mergedMessages.Last().Content += "\n\n" + m.message;
            }
            else
            {
                mergedMessages.Add(new Message
                {
                    Role = roleStr,
                    Content = m.message
                });
            }
        }

        return JsonUtil.SerializeToJson(new Player2Request
        {
            Messages = mergedMessages,
            Stream = stream
        });
    }

    private static string RoleToString(Role role)
    {
        return role switch
        {
            Role.System => "system",
            Role.User => "user",
            Role.AI => "assistant",
            _ => "user"
        };
    }

    private async Task<string> SendRequestAsync(string url, string jsonContent, Player2StreamHandler streamHandler = null)
    {
        Logger.Debug($"Player2 Request ({(_isLocalConnection ? "local" : "remote")}): {url}\n{jsonContent}");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = $"Bearer {_apiKey}",
            [Player2GameKeys.HeaderName] = Player2GameKeys.Canonical
        };

        using var cts = new CancellationTokenSource();
        var send = SharedHttpTransport.Current.SendAsync(
            new HttpTransportRequest
            {
                Method = "POST",
                Url = url,
                Headers = headers,
                Body = jsonContent,
                ContentType = "application/json",
                TimeoutMilliseconds = 120000,
                FirstByteTimeoutMilliseconds = 60000,
                IdleTimeoutMilliseconds = 60000,
                CorrelationId = "communication-player2"
            },
            streamHandler == null ? (Action<string>)null : streamHandler.AppendUtf8,
            cts.Token);

        while (!send.IsCompleted)
        {
            if (Current.Game == null)
            {
                cts.Cancel();
                return null;
            }

            await Task.Delay(100);
        }

        HttpTransportResponse http = await send;

        if (streamHandler != null)
        {
            streamHandler.Flush();

            if (!string.IsNullOrEmpty(streamHandler.DetectedError))
            {
                string errorMsg = streamHandler.DetectedError;
                string allText = streamHandler.GetAllReceivedText();

                if (errorMsg.Contains("ResourceExhausted") || errorMsg.Contains("Insufficient"))
                {
                    throw new QuotaExceededException("Player2 quota exceeded",
                        new Payload(url, null, jsonContent, allText, 0, errorMsg));
                }

                throw new AIRequestException(errorMsg, new Payload(url, null, jsonContent, allText, 0, errorMsg));
            }
        }

        if (http.TimedOut)
            throw new TimeoutException(http.ErrorMessage ?? "Request timed out");

        if (http.Cancelled)
            return null;

        string responseText = !string.IsNullOrEmpty(http.BodyText)
            ? http.BodyText
            : streamHandler?.GetAllReceivedText();

        if (!http.Succeeded)
        {
            string errorMsg = ErrorUtil.ExtractErrorMessage(responseText) ?? http.ErrorMessage;
            Logger.Error($"Player2 failed: {http.StatusCode} - {errorMsg}");
            if (http.StatusCode == 401)
                Player2Session.Current.Invalidate("communication-401");
            throw new AIRequestException(errorMsg, new Payload(url, null, jsonContent, responseText, 0, errorMsg));
        }

        if (streamHandler == null)
            Logger.Debug($"Player2 Response: \n{responseText}");
        else
            Logger.Debug($"Player2 Streaming complete. Tokens: {streamHandler.GetTotalTokens()}");

        return responseText;
    }

    static void ShowNotification(string messageKey, MessageTypeDef type)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            try
            {
                bool isDetected = messageKey == "RimTalk.Player2.LocalDetected";
                string text = isDetected
                    ? "RimTalk: Player2 desktop app detected! Using automatic authentication (no API key needed)."
                    : "RimTalk: Player2 desktop app not found. Please start app or add API key manually.";

                Messages.Message(text, type);
                Logger.Message(isDetected
                    ? "RimTalk: ✓ Successfully connected to local Player2 app"
                    : "RimTalk: Player2 local app not available, manual API key required");
            }
            catch
            {
                /* Ignore UI errors */
            }
        });
    }

    public static void StopHealthCheck()
    {
        // Health cache lives on the canonical Player2 session.
    }

    public static void CheckPlayer2StatusAndNotify()
    {
        Task.Run(() =>
        {
            bool isAvailable = Player2Session.Current.ProbeHealth(Player2EndpointKind.LocalApp, force: true).Healthy;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (isAvailable)
                    Messages.Message("RimTalk: Player2 desktop app detected!", MessageTypeDefOf.PositiveEvent);
                else
                    Messages.Message("RimTalk: Player2 desktop app not detected.", MessageTypeDefOf.CautionInput);
            });
        });
    }
}
