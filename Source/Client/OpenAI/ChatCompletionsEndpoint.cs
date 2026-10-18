using System;

namespace Ustas.RimAI.Communication.Client.OpenAI;

/// <summary>
/// Turns the base URL a player types for an OpenAI-compatible provider into the chat completions
/// endpoint. A bare host gets <c>/v1/chat/completions</c>, a base that stops at <c>/v1</c> gets
/// <c>/chat/completions</c>, and anything else is taken as the full endpoint. Trailing slashes
/// are dropped and a query string stays at the end.
/// </summary>
public static class ChatCompletionsEndpoint
{
    public const string VersionSegment = "/v1";
    public const string ChatCompletionsPath = "/chat/completions";

    public static string Format(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;

        string trimmed = baseUrl.Trim().TrimEnd('/');
        int cut = trimmed.IndexOfAny(['?', '#']);
        string head = (cut < 0 ? trimmed : trimmed.Substring(0, cut)).TrimEnd('/');
        string tail = cut < 0 ? string.Empty : trimmed.Substring(cut);

        if (!Uri.TryCreate(head, UriKind.Absolute, out var uri))
            return head + tail;

        string path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length == 0)
            return head + VersionSegment + ChatCompletionsPath + tail;
        if (path.EndsWith(VersionSegment, StringComparison.OrdinalIgnoreCase))
            return head + ChatCompletionsPath + tail;

        return head + tail;
    }
}
