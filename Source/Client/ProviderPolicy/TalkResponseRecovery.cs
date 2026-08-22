using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Client.ProviderPolicy;

public enum TalkResponseEvaluation
{
    Empty = 0,
    Malformed = 1,
    Recovered = 2
}

public sealed class RecoveredTalkObject
{
    public string Name { get; set; }
    public string Text { get; set; }
}

/// <summary>
/// Recovers valid talk objects from mixed or garbage LLM text. A recovered
/// object must have both name and text. Empty or malformed input is never
/// treated as a successful talk response.
/// </summary>
public static class TalkResponseRecovery
{
    static readonly Regex NamePattern = new Regex("\"name\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);
    static readonly Regex TextPattern = new Regex("\"text\"\\s*:\\s*\"(?<v>(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.CultureInvariant);

    public static TalkResponseEvaluation Evaluate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return TalkResponseEvaluation.Empty;
        return Extract(raw).Count > 0 ? TalkResponseEvaluation.Recovered : TalkResponseEvaluation.Malformed;
    }

    public static IReadOnlyList<RecoveredTalkObject> Extract(string raw)
    {
        var recovered = new List<RecoveredTalkObject>();
        if (string.IsNullOrWhiteSpace(raw))
            return recovered;

        var text = raw.Replace("```json", string.Empty).Replace("```", string.Empty);
        var searchStart = 0;
        while (searchStart < text.Length)
        {
            var objStart = text.IndexOf('{', searchStart);
            if (objStart < 0)
                break;
            var objEnd = FindMatchingBrace(text, objStart);
            if (objEnd < 0)
                break;

            var jsonObj = text.Substring(objStart, objEnd - objStart + 1);
            var name = MatchValue(NamePattern, jsonObj);
            var talk = MatchValue(TextPattern, jsonObj);
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(talk))
            {
                recovered.Add(new RecoveredTalkObject { Name = Unescape(name), Text = Unescape(talk) });
            }

            searchStart = objEnd + 1;
        }

        return recovered;
    }

    static string MatchValue(Regex regex, string json)
    {
        var match = regex.Match(json ?? string.Empty);
        return match.Success ? match.Groups["v"].Value : null;
    }

    static string Unescape(string value) =>
        (value ?? string.Empty)
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");

    static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = openIndex; i < text.Length; i++)
        {
            var c = text[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }
}
