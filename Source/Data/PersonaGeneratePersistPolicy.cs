using System.Collections.Generic;
using Ustas.RimAI.Core.Personas;

namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// Authoritative generate → persist → talk path for per-pawn personalities.
/// Empty or whitespace generation does not persist and does not inject talk.
/// </summary>
public static class PersonaGeneratePersistPolicy
{
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return raw.Replace("**", string.Empty).Trim();
    }

    public static bool TryAcceptGenerated(string raw, out string sanitized)
    {
        sanitized = Sanitize(raw);
        return sanitized.Length > 0;
    }

    public static bool Persist(IDictionary<string, string> store, string pawnId, string raw)
    {
        if (store == null || string.IsNullOrEmpty(pawnId))
            return false;
        if (!TryAcceptGenerated(raw, out string sanitized))
            return false;
        store[pawnId] = sanitized;
        return true;
    }

    public static string Read(IDictionary<string, string> store, string pawnId)
    {
        if (store == null || string.IsNullOrEmpty(pawnId))
            return string.Empty;
        return store.TryGetValue(pawnId, out string value) ? value ?? string.Empty : string.Empty;
    }

    public static bool ShouldInjectTalk(string stored) =>
        !string.IsNullOrEmpty(stored);

    public static string FormatTalkLine(string stored)
    {
        if (!ShouldInjectTalk(stored))
            return string.Empty;
        return PersonaProjectionDefaults.FormatTalkPersonalityLine(stored);
    }
}
