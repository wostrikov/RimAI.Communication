using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Error;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.AI;
using Ustas.RimAI.Core.Net;
using Verse;
using Enumerable = System.Linq.Enumerable;
using RimAI.Core.Runtime;

namespace Ustas.RimAI.Communication.Client.OpenAI;

public class OpenAIClient(
    string baseUrl,
    string model,
    string apiKey = null,
    Dictionary<string, string> extraHeaders = null,
    bool officialOpenAI = false)
    : IAIClient
{
    private const string DefaultPath = "/v1/chat/completions";
    private readonly string _endpointUrl = FormatEndpointUrl(baseUrl);
    private readonly Random _random = new();

    private static string FormatEndpointUrl(string baseUrl)
    {
        if (string.IsNullOrEmpty(baseUrl)) return string.Empty;
        var trimmed = baseUrl.Trim().TrimEnd('/');
        var uri = new Uri(trimmed);
        // Append default path if only base domain is provided
        return (uri.AbsolutePath == "/" || string.IsNullOrEmpty(uri.AbsolutePath.Trim('/')))
            ? trimmed + DefaultPath
            : trimmed;
    }

    public async Task<Payload> GetChatCompletionAsync(List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages,
        Action<Payload> onRequestPrepared = null)
    {
        string jsonContent = officialOpenAI ? BuildResponsesJson(prefixMessages, messages) : BuildRequestJson(prefixMessages, messages, stream: false);
        onRequestPrepared?.Invoke(new Payload(_endpointUrl, model, jsonContent, null, 0));
        var shared = await RimAiBackground.Run(() => SharedTextAiOrchestrator.Complete(new TextAiRequest
        {
            Messages = ToSharedMessages(prefixMessages, messages),
            Model = model,
            BaseUrl = _endpointUrl,
            ApiShape = officialOpenAI ? TextAiApiShape.Responses : TextAiApiShape.ChatCompletions,
            UseSharedGameplayCredential = officialOpenAI,
            ApiKey = officialOpenAI ? null : apiKey,
            ExtraHeaders = extraHeaders,
            PrebuiltJson = jsonContent,
            Caller = "communication",
            Arbitration = AiRequestMetadata.FromCaller("communication")
        }));
        if (shared.StatusCode == 429)
        {
            throw new QuotaExceededException(shared.Error ?? "Quota exceeded",
                new Payload(_endpointUrl, model, jsonContent, shared.RawPayload, 0, shared.Error));
        }
        if (!shared.Succeeded)
        {
            throw new AIRequestException(shared.Error ?? "Request failed",
                new Payload(_endpointUrl, model, jsonContent, shared.RawPayload, 0, shared.Error));
        }

        string responseText = shared.RawPayload;
        var response = officialOpenAI ? null : JsonUtil.DeserializeFromJson<OpenAIResponse>(responseText);
        var content = officialOpenAI ? shared.Text : response?.Choices?[0]?.Message?.Content ?? shared.Text;
        var tokens = officialOpenAI ? ParseResponsesTotalTokens(responseText) : response?.Usage?.TotalTokens ?? 0;

        return new Payload(_endpointUrl, model, jsonContent, content, tokens);
    }

    public async Task<Payload> GetStreamingChatCompletionAsync<T>(List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages,
        Action<T> onResponseParsed,
        Action<Payload> onRequestPrepared = null) where T : class
    {
        if (officialOpenAI)
        {
            Payload result = await GetChatCompletionAsync(prefixMessages, messages, onRequestPrepared);
            foreach (var parsed in new JsonStreamParser<T>().Parse(result.Response)) onResponseParsed?.Invoke(parsed);
            return result;
        }
        string jsonContent = BuildRequestJson(prefixMessages, messages, stream: true);
        onRequestPrepared?.Invoke(new Payload(_endpointUrl, model, jsonContent, null, 0));
        var jsonParser = new JsonStreamParser<T>();

        var streamHandler = new OpenAIStreamHandler(chunk =>
        {
            foreach (var response in jsonParser.Parse(chunk))
                onResponseParsed?.Invoke(response);
        });

        await SendRequestAsync(jsonContent, streamHandler);

        return new Payload(_endpointUrl, model, jsonContent, streamHandler.GetFullText(),
            streamHandler.GetTotalTokens());
    }

    private string BuildResponsesJson(List<(Role role, string message)> prefixMessages, List<(Role role, string message)> messages)
    {
        var all = new List<(Role role, string message)>();
        if (prefixMessages != null) all.AddRange(prefixMessages);
        if (messages != null) all.AddRange(messages);
        return OpenAIProviderAdapter.BuildRequest(model, all);
    }

    private static int ParseResponsesTotalTokens(string json)
    {
        var match = System.Text.RegularExpressions.Regex.Match(json ?? "", "\\\"total_tokens\\\"\\s*:\\s*(?<n>\\d+)");
        return match.Success && int.TryParse(match.Groups["n"].Value, out int value) ? value : 0;
    }

    private string BuildRequestJson(List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages, bool stream)
    {
        var rawMessages = new List<(Role role, string message)>();
        if (prefixMessages != null) rawMessages.AddRange(prefixMessages);
        if (messages != null) rawMessages.AddRange(messages);

        var mergedMessages = new List<Message>();

        bool isGemma3 = !string.IsNullOrEmpty(model) && model.Contains("gemma-3");
        if (isGemma3)
        {
            var systemMessages = Enumerable.ToList(Enumerable.Where(rawMessages, m => m.role == Role.System));
            if (systemMessages.Any())
            {
                var systemText = string.Join("\n\n", Enumerable.Select(systemMessages, m => m.message));

                mergedMessages.Add(new Message
                {
                    Role = "user",
                    Content = $"{_random.Next()} {systemText}"
                });
                rawMessages.RemoveAll(m => m.role == Role.System);
            }
        }

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
        
        string? reasoningEffort = DefaultReasoningEffort(model);

        var request = new OpenAIRequest
        {
            Model = model,
            Messages = mergedMessages,
            Stream = stream,
            StreamOptions = stream ? new StreamOptions { IncludeUsage = true } : null,
            ReasoningEffort = reasoningEffort
        };

        return JsonUtil.SerializeToJson(request);
    }

    /// <summary>
    /// Talk lines want an answer, not deliberation. Every current Gemini takes "low" - the
    /// newer Flash models reject "minimal" - and Gemma thinks least with "minimal".
    /// </summary>
    internal static string? DefaultReasoningEffort(string? model)
    {
        if (string.IsNullOrEmpty(model)) return null;
        string m = model.ToLowerInvariant();
        if (m.Contains("gemini")) return "low";
        if (m.Contains("gemma")) return "minimal";
        return null;
    }

    static List<TextAiMessage> ToSharedMessages(
        List<(Role role, string message)> prefixMessages,
        List<(Role role, string message)> messages)
    {
        var result = new List<TextAiMessage>();
        if (prefixMessages != null)
        {
            foreach (var item in prefixMessages)
                result.Add(new TextAiMessage(RoleToString(item.role), item.message));
        }
        if (messages != null)
        {
            foreach (var item in messages)
                result.Add(new TextAiMessage(RoleToString(item.role), item.message));
        }
        return result;
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

    private async Task<string> SendRequestAsync(string jsonContent, OpenAIStreamHandler streamHandler)
    {
        if (string.IsNullOrEmpty(_endpointUrl))
        {
            Logger.Error("Endpoint URL is missing.");
            return null;
        }

        Logger.Debug($"API request: {_endpointUrl}");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(apiKey))
            headers["Authorization"] = $"Bearer {apiKey}";
        if (extraHeaders != null)
        {
            foreach (var header in extraHeaders)
                headers[header.Key] = header.Value;
        }

        bool isLocal = _endpointUrl.Contains("localhost") || _endpointUrl.Contains("127.0.0.1") ||
                       _endpointUrl.Contains("192.168.") || _endpointUrl.Contains("10.");
        float connectTimeout = isLocal ? 300f : 60f;
        const float readTimeout = 60f;

        using var cts = new CancellationTokenSource();
        var send = SharedHttpTransport.Current.SendAsync(
            new HttpTransportRequest
            {
                Method = "POST",
                Url = _endpointUrl,
                Headers = headers,
                Body = jsonContent,
                ContentType = "application/json",
                TimeoutMilliseconds = (int)((connectTimeout + readTimeout) * 1000),
                FirstByteTimeoutMilliseconds = (int)(connectTimeout * 1000),
                IdleTimeoutMilliseconds = (int)(readTimeout * 1000),
                CorrelationId = "communication-openai"
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

            if (Service.AIService.IsCancellationRequested())
            {
                cts.Cancel();
                throw new OperationCanceledException("Cancelled for a more urgent talk.");
            }

            await Task.Delay(100);
        }

        HttpTransportResponse http = await send;
        string responseText = http.BodyText;

        if (streamHandler != null)
        {
            if (!string.IsNullOrEmpty(streamHandler.DetectedError))
            {
                string errorMsg = streamHandler.DetectedError;
                string allText = streamHandler.GetAllReceivedText();
                throw new AIRequestException(errorMsg,
                    new Payload(_endpointUrl, model, jsonContent, allText, 0, errorMsg));
            }

            if (http.StatusCode >= 400 || !http.Succeeded)
            {
                responseText = streamHandler.GetAllReceivedText();
                if (string.IsNullOrEmpty(responseText)) responseText = streamHandler.GetRawJson();
            }
        }

        if (http.TimedOut)
            throw new TimeoutException(http.ErrorMessage ?? "Request timed out");

        if (http.Cancelled)
            return null;

        if (http.StatusCode == 429)
        {
            string errorMsg = ErrorUtil.ExtractErrorMessage(responseText) ?? "Quota exceeded";
            throw new QuotaExceededException(errorMsg,
                new Payload(_endpointUrl, model, jsonContent, responseText, 0, errorMsg));
        }

        if (!http.Succeeded)
        {
            string errorMsg = ErrorUtil.ExtractErrorMessage(responseText) ?? http.ErrorMessage;
            Logger.Error($"Request failed: {http.StatusCode} - {errorMsg}");
            throw new AIRequestException(errorMsg,
                new Payload(_endpointUrl, model, jsonContent, responseText, 0, errorMsg));
        }

        Logger.Debug($"API response received: HTTP {http.StatusCode}");
        return responseText;
    }

    public static async Task<List<string>> FetchModelsAsync(string apiKey, string url)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(apiKey))
            headers["Authorization"] = "Bearer " + apiKey;

        var http = await SharedHttpTransport.Current.SendAsync(new HttpTransportRequest
        {
            Method = "GET",
            Url = url,
            Headers = headers,
            TimeoutMilliseconds = 60000,
            CorrelationId = "communication-openai-models"
        });

        if (!http.Succeeded)
        {
            Logger.Error($"Failed to fetch models: {http.ErrorMessage}");
            return new List<string>();
        }

        var response = JsonUtil.DeserializeFromJson<OpenAIModelsResponse>(http.BodyText);
        return response?.Data?.Select(m => m.Id).ToList() ?? new List<string>();
    }
}
