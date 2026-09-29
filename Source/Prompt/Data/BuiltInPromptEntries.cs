using System;
using System.Linq;

namespace Ustas.RimAI.Communication.Prompt;

/// <summary>
/// The entries every default preset starts with, known by a fixed id so that renaming one in the
/// preset editor does not lose it. Presets saved before the ids existed carry random ids, so each
/// lookup falls back to the entry's original name - nothing already saved has to be migrated.
/// </summary>
public static class BuiltInPromptEntries
{
    public const string BaseInstructionId = "builtin-base-instruction";
    public const string JsonFormatId = "builtin-json-format";
    public const string PawnProfilesId = "builtin-pawn-profiles";
    public const string ChatHistoryId = "builtin-chat-history";
    public const string DialoguePromptId = "builtin-dialogue-prompt";

    public const string BaseInstructionName = "Base Instruction";
    public const string JsonFormatName = "JSON Format";
    public const string PawnProfilesName = "Pawn Profiles";
    public const string ChatHistoryName = "Chat History";
    public const string DialoguePromptName = "Dialogue Prompt";

    /// <summary>Whether the id is one of the fixed ones, which a copied entry keeps.</summary>
    public static bool IsBuiltInId(string id) => id != null && id.StartsWith("builtin-", StringComparison.Ordinal);

    public static PromptEntry FindBaseInstruction(PromptPreset preset) =>
        Find(preset, BaseInstructionId, BaseInstructionName);

    public static PromptEntry FindJsonFormat(PromptPreset preset) =>
        Find(preset, JsonFormatId, JsonFormatName);

    /// <summary>
    /// The preset's Base Instruction: by id, by name, else its first system entry; one is
    /// added at the top when it has none.
    /// </summary>
    public static PromptEntry GetOrCreateBaseInstruction(PromptPreset preset, string defaultContent)
    {
        if (preset == null) return null;

        var entry = FindBaseInstruction(preset)
                    ?? preset.Entries.FirstOrDefault(e => e.Role == PromptRole.System && e.Position == PromptPosition.Relative);
        if (entry != null) return entry;

        entry = new PromptEntry
        {
            Id = BaseInstructionId,
            Name = BaseInstructionName,
            Role = PromptRole.System,
            Position = PromptPosition.Relative,
            Content = defaultContent
        };
        preset.Entries.Insert(0, entry);
        return entry;
    }

    private static PromptEntry Find(PromptPreset preset, string id, string name)
    {
        if (preset?.Entries == null) return null;
        return preset.Entries.FirstOrDefault(e => e.Id == id)
               ?? preset.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
