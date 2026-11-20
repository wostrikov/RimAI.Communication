using System.Collections.Generic;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.Client.OpenAI;

namespace Ustas.RimAI.Communication.Util;

public static class ErrorUtil
{
    public static string ExtractErrorMessage(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse)) return null;

        // 1. Try standard wrapped ErrorResponse { "error": { ... } }
        if (JsonUtil.TryDeserializeFromJson<ErrorResponse>(jsonResponse, out var wrapped, out _))
        {
            if (wrapped?.Error != null) return FormatError(wrapped.Error);
        }

        // 2. Try flat ErrorDetail { "message": "...", "code": ... }
        if (JsonUtil.TryDeserializeFromJson<ErrorDetail>(jsonResponse, out var flat, out _))
        {
            if (!string.IsNullOrEmpty(flat.Message)) return FormatError(flat);
        }

        // 3. Try array-wrapped [ { "error": ... } ]
        if (JsonUtil.TryDeserializeFromJson<List<ErrorResponse>>(jsonResponse, out var list, out _))
        {
            if (list != null && list.Count > 0 && list[0].Error != null)
            {
                return FormatError(list[0].Error);
            }
        }

        // 4. Read the fields directly. OpenAI's "code" is a string ("model_not_found",
        // "invalid_api_key"), ErrorDetail.Code is the int other providers send, and a
        // string there fails the whole contract read above - so OpenAI's own explanation
        // of a failed request was dropped and the request logged as Unknown.
        string message = ReadStringField(jsonResponse, "message");
        if (string.IsNullOrEmpty(message)) return null;
        string code = ExtractErrorCode(jsonResponse);
        return string.IsNullOrEmpty(code) ? message : $"[{code}] {message}";
    }

    /// <summary>The error's "code", string or number, or null.</summary>
    public static string ExtractErrorCode(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse)) return null;
        var match = Regex.Match(jsonResponse, "\"code\"\\s*:\\s*(?:\"((?:\\\\.|[^\"\\\\])*)\"|(-?\\d+))");
        if (!match.Success) return null;
        return match.Groups[1].Success ? Unescape(match.Groups[1].Value) : match.Groups[2].Value;
    }

    /// <summary>
    /// An error text with anything shaped like an API key cut down to its prefix. A
    /// provider's 401 quotes the key it was given - masked by OpenAI, but still part of
    /// one - and these texts go to the log and to the player.
    /// </summary>
    public static string RedactKeys(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, "\\b(sk|pk|rk)-[A-Za-z0-9*_\\-]{4,}", "$1-…");
    }

    static string ReadStringField(string json, string field)
    {
        var match = Regex.Match(json, "\"" + field + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        return match.Success ? Unescape(match.Groups[1].Value) : null;
    }

    static string Unescape(string value)
    {
        try
        {
            return Regex.Unescape(value);
        }
        catch (System.ArgumentException)
        {
            return value;
        }
    }

    private static string FormatError(ErrorDetail detail)
    {
        if (detail == null) return null;

        string msg = detail.Message;
        if (string.IsNullOrEmpty(msg)) msg = detail.Status;
        if (string.IsNullOrEmpty(msg)) msg = detail.Type;
        if (string.IsNullOrEmpty(msg)) return null;

        if (detail.Code != 0)
        {
            return $"[{detail.Code}] {msg}";
        }

        if (!string.IsNullOrEmpty(detail.Status) && detail.Status != msg)
        {
            return $"({detail.Status}) {msg}";
        }

        return msg;
    }
}
