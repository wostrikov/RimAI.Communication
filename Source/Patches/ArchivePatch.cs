using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;
using RimWorld;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Patches;

[HarmonyPatch(typeof(Archive), nameof(Archive.Add))]
public static class ArchivePatch
{
    public static void Prefix(IArchivable archivable)
    {
        if (!ShouldProcessArchivable(archivable))
        {
            return;
        }

        // Generate the prompt text first, as it's needed in all cases.
        // Decide quest category & generate prompt (kept compatible with original text)
        var (prompt, talkType) = GeneratePrompt(archivable);
        var eventMap = FindLocation(archivable);

        // One request for the colony, taken by whichever colonist speaks next. A request for every
        // colonist on the map had each of them bring up the same letter in turn.
        TalkRequestPool.Add(prompt, mapId: eventMap?.uniqueID ?? -1, talkType: talkType);
    }

    private static bool ShouldProcessArchivable(IArchivable archivable)
    {
        var settings = Settings.Get();
        var enabledTypes = settings.EnabledArchivableTypes;

        // Messages - "research finished", "a colonist is hungry" - are off unless switched on. The
        // event filter page wrote that default only once it had been opened, so on a game where
        // nobody had opened it every message reached the talk pool.
        if (archivable is Message message)
        {
            if (!enabledTypes.TryGetValue("Verse.Message", out var messagesEnabled) || !messagesEnabled)
                return false;

            return message.def == null
                || !enabledTypes.TryGetValue(message.def.defName, out var isMessageDefEnabled)
                || isMessageDefEnabled;
        }

        // Letters and everything else are on unless switched off, by C# type and then by def.
        string typeName = archivable.GetType().FullName;
        if (enabledTypes.TryGetValue(typeName, out var isTypeEnabled) && !isTypeEnabled)
            return false;

        if (archivable is Letter letter && letter.def != null
            && enabledTypes.TryGetValue(letter.def.defName, out var isDefEnabled) && !isDefEnabled)
            return false;

        return true;
    }

    private static (string prompt, TalkType talkType) GeneratePrompt(IArchivable archivable)
    {
        string label = archivable.ArchivedLabel?.StripTags();
        string tip = archivable.ArchivedTooltip?.StripTags() ?? string.Empty;

        if (archivable is ChoiceLetter { quest: not null } choiceLetter)
        {
            return choiceLetter.quest.State == QuestState.NotYetAccepted
                ? (EventPromptPolicy.Compose("Talk if you want to accept quest", label,
                    choiceLetter.quest.description.ToString().StripTags()), TalkType.QuestOffer)
                : (EventPromptPolicy.Compose("Talk about quest result", label, tip), TalkType.QuestEnd);
        }

        if (archivable is Letter and not ChoiceLetter && ContainsQuestReference(label ?? string.Empty, tip))
            return (EventPromptPolicy.Compose("Talk about quest result", label, tip), TalkType.QuestEnd);

        return (EventPromptPolicy.Compose("Talk about incident", label, tip, GetTargetSuffix(archivable)), TalkType.Event);
    }

    /// <summary>
    /// Who a letter is about, when it is about a pawn. The tooltip often says "a colonist" or a
    /// name the model cannot tie to anyone in the talk.
    /// </summary>
    private static string GetTargetSuffix(IArchivable archivable)
    {
        var pawn = archivable?.LookTargets?.PrimaryTarget.Thing as Pawn
            ?? archivable?.LookTargets?.targets?.Select(t => t.Thing as Pawn).FirstOrDefault(p => p != null);

        return pawn != null ? $" (Target: {pawn.LabelShort})" : string.Empty;
    }

    private static bool ContainsQuestReference(string label, string tip)
    {
        return label.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0
            || tip.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Map FindLocation(IArchivable archivable)
    {
        if (archivable.LookTargets is not { Any: true })
            return null;

        return archivable.LookTargets.PrimaryTarget.Map
            ?? archivable.LookTargets.targets.Select(t => t.Map).FirstOrDefault(m => m != null);
    }
}
