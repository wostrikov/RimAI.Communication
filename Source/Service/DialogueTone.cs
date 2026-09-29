using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Util;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// How a pawn should sound toward the one it talks to: cold toward someone it dislikes, reserved
/// toward someone it has never met, grudging toward a patient it cannot stand, and in a fight
/// clear about who is on which side. Each returns one line of the dialogue prompt, or null.
/// </summary>
public static class DialogueTone
{
    /// <summary>The line about the partner: dislike first, then a first meeting.</summary>
    public static string TowardPartner(Pawn mainPawn, Pawn partner, List<Pawn> pawns, bool isFirstMeeting)
    {
        if (mainPawn == null || partner == null) return null;
        string name = PromptService.GetUniqueName(partner, pawns);

        if (RelationsService.HasFriction(mainPawn, partner, out bool isSevere))
            return isSevere
                ? $"Tone toward {name}: bitter, resentful or hostile - a deep grudge. Let the tension show the way this personality would show it."
                : $"Tone toward {name}: guarded, curt or distant - there is friction between them. Reserved, not openly aggressive.";

        return isFirstMeeting
            ? $"Tone toward {name}: they have never met - a brief greeting, an introduction or a cautious question; reserved, not familiar."
            : null;
    }

    /// <summary>A pawn tending or hauling someone it dislikes does it, and does not pretend to like it.</summary>
    public static string TowardPatient(Pawn mainPawn, List<Pawn> pawns)
    {
        if (!mainPawn.IsCaringFor(out var patient) || !RelationsService.HasFriction(mainPawn, patient, out _))
            return null;
        string name = PromptService.GetUniqueName(patient, pawns);
        return $"Tone toward {name}: reluctant or strictly professional - caring for them out of duty, despite the friction between them.";
    }

    /// <summary>
    /// Whether the two meet for the first time: someone from outside the colony, with no family
    /// tie, no liking and no shared memory of an earlier talk.
    /// </summary>
    public static bool IsFirstMeeting(Pawn mainPawn, Pawn partner)
    {
        if (mainPawn == null || partner == null || partner.IsPlayer() || mainPawn.IsPlayer()) return false;
        if (!RelationsService.IsOutsiderOrStranger(mainPawn, partner)) return false;
        if (mainPawn.GetMostImportantRelation(partner) != null) return false;
        if (mainPawn.relations != null && mainPawn.relations.OpinionOf(partner) >= 20) return false;
        return !RemembersMeeting(mainPawn, partner) && !RemembersMeeting(partner, mainPawn);
    }

    private static bool RemembersMeeting(Pawn pawn, Pawn other)
    {
        var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
        return memories != null && memories.Any(m => m.otherPawn == other);
    }

    /// <summary>
    /// Who stands on which side, when the talk has people of both: without it the model had
    /// colonists giving orders to raiders and raiders cheering on colonists.
    /// </summary>
    public static bool TryDescribeCombatSides(Pawn mainPawn, List<Pawn> pawns, out string sides)
    {
        sides = null;
        if (pawns is not { Count: > 1 }) return false;

        var allies = new List<string>();
        var enemies = new List<string>();
        foreach (var p in pawns)
        {
            if (p == null || p.IsPlayer()) continue;
            (p == mainPawn || !p.HostileTo(mainPawn) ? allies : enemies).Add(PromptService.GetUniqueName(p, pawns));
        }

        if (enemies.Count == 0) return false;

        string ownSide = mainPawn.IsFreeColonist ? "Colonists" : "Allies";
        sides = $"[Combat sides]\n- {ownSide}: {string.Join(", ", allies)}\n- Enemies: {string.Join(", ", enemies)}\n" +
                "(Combat shouting: orders only to their own side; taunts, threats or calls to surrender across the line)";
        return true;
    }

    /// <summary>The instruction for a pawn in a mental break, fixed on a topic when it has one.</summary>
    public static string MentalBreak(Pawn mainPawn, string fixation)
    {
        string label = mainPawn.MentalStateDef?.label ?? "distressed";
        string line = $"In a mental break ({label}): raw, unstable emotion; let the outburst shift in focus and never repeat a phrase.";
        return fixation == null
            ? line
            : $"{line}\nObsessive thought: {fixation} - woven through the break, erratically.";
    }
}
