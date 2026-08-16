using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RimTalk.Data;
using Ustas.RimAI.Core.Configuration;

namespace RimTalk.Client.OpenAI;

public enum OpenAIErrorCategory { Unknown, Authentication, Permission, InvalidRequest, UnsupportedParameter, ModelNotFound, RateLimit, Server }

public static class OpenAIProviderAdapter
{
    public const string CredentialVariable = AiCredentialResolver.Canonical;
    public const string ResponsesEndpoint = "https://api.openai.com/v1/responses";
    public const string ModelsEndpoint = "https://api.openai.com/v1/models";
    public static string ResolveCredential() => AiCredentialResolver.Resolve().Value ?? string.Empty;
    public static bool CredentialPresent => AiCredentialResolver.Resolve().Present;
    public static string CredentialDisplay => AiCredentialResolver.Resolve().Display;

    public static string BuildRequest(string model, IList<(Role role, string message)> messages, int maxOutputTokens = 2048)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("OpenAI model is required.");
        if (messages == null || messages.Count == 0) throw new ArgumentException("OpenAI input is required.");
        var json = new StringBuilder("{\"model\":\"").Append(Escape(model)).Append("\",\"input\":[");
        for (int i = 0; i < messages.Count; i++)
        {
            if (i > 0) json.Append(',');
            string role = messages[i].role == Role.System ? "developer" : messages[i].role == Role.AI ? "assistant" : "user";
            json.Append("{\"role\":\"").Append(role).Append("\",\"content\":[{\"type\":\"input_text\",\"text\":\"")
                .Append(Escape(messages[i].message)).Append("\"}]}");
        }
        return json.Append("],\"max_output_tokens\":").Append(maxOutputTokens).Append(",\"store\":false}").ToString();
    }

    public static string ParseOutputText(string json)
    {
        var values = new List<string>();
        foreach (Match item in Regex.Matches(json ?? "", "\\{(?<o>(?:[^{}\\\"]|\\\"(?:[^\\\"\\\\]|\\\\.)*\\\")*)\\}"))
            if (Regex.IsMatch(item.Groups["o"].Value, "\\\"type\\\"\\s*:\\s*\\\"output_text\\\""))
            { var text = Regex.Match(item.Groups["o"].Value, "\\\"text\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)*)\\\""); if (text.Success) values.Add(Unescape(text.Groups["v"].Value)); }
        return string.Join(" ", values).Trim();
    }

    public static List<string> ParseModels(string json)
    {
        var result = new List<string>();
        foreach (Match m in Regex.Matches(json ?? "", "\\\"id\\\"\\s*:\\s*\\\"(?<v>(?:[^\\\"\\\\]|\\\\.)+)\\\"")) result.Add(Unescape(m.Groups["v"].Value));
        if (result.Count == 0) throw new FormatException("Models response contains no model ids.");
        return result;
    }

    public static OpenAIErrorCategory Classify(long status, string body)
    {
        string marker = (body ?? "").ToLowerInvariant();
        if (marker.Contains("unsupported_parameter")) return OpenAIErrorCategory.UnsupportedParameter;
        if (marker.Contains("model_not_found")) return OpenAIErrorCategory.ModelNotFound;
        if (status == 401) return OpenAIErrorCategory.Authentication;
        if (status == 403) return OpenAIErrorCategory.Permission;
        if (status == 429) return OpenAIErrorCategory.RateLimit;
        if (status >= 500) return OpenAIErrorCategory.Server;
        if (status == 400) return OpenAIErrorCategory.InvalidRequest;
        return OpenAIErrorCategory.Unknown;
    }
    private static string Escape(string v) => (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    private static string Unescape(string v) => (v ?? "").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
}
