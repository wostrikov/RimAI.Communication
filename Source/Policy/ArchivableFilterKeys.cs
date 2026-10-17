using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// The keys of the event filter. Letter defs and archivable types are stored under their own
/// names; message defs under "Verse.Message:defName". Vanilla has MessageTypeDefs and LetterDefs
/// with the same names (ThreatBig, NegativeEvent, ...), and one shared key let the message
/// default - off - switch the raid letters off with it. Host-free, so it can be tested without
/// the game.
/// </summary>
public static class ArchivableFilterKeys
{
    /// <summary>The C# type of every message, and the parent row of the message defs.</summary>
    public const string MessageParent = "Verse.Message";

    const string MessagePrefix = MessageParent + ":";

    public static string ForMessageDef(string defName) => MessagePrefix + defName;

    /// <summary>The message parent itself or one of its def keys; these default to off.</summary>
    public static bool IsMessageKey(string key) =>
        key != null && (key == MessageParent || key.StartsWith(MessagePrefix, StringComparison.Ordinal));

    /// <summary>What the filter page shows for a key: a message def without its prefix.</summary>
    public static string DisplayName(string key) =>
        key != null && key.StartsWith(MessagePrefix, StringComparison.Ordinal)
            ? key.Substring(MessagePrefix.Length)
            : key;

    /// <summary>
    /// Moves the value stored under a message def's bare name to its prefixed key, unless the
    /// prefixed key already has one. The bare entry goes in every case: for a message-only def
    /// nothing reads it any more, and for a name that is also a LetterDef it held the message's
    /// value, so the letter returns to its own default rather than inherit it.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public static bool MigrateBareMessageKeys(IDictionary<string, bool> enabled, IEnumerable<string> messageDefNames)
    {
        if (enabled == null || messageDefNames == null)
            return false;

        bool changed = false;
        foreach (string defName in messageDefNames)
        {
            if (string.IsNullOrEmpty(defName) || !enabled.TryGetValue(defName, out bool value))
                continue;

            string key = ForMessageDef(defName);
            if (!enabled.ContainsKey(key))
                enabled[key] = value;
            enabled.Remove(defName);
            changed = true;
        }

        return changed;
    }
}
