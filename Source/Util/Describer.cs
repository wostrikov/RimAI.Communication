using System;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Util;

/// <summary>
/// Words for the numbers in the pawn context. These feed the model, not the UI, so they stay
/// English whatever the game language, like the rest of the context scaffolding.
/// </summary>
public static class Describer
{
    public static string Wealth(float wealthTotal)
    {
        return wealthTotal switch
        {
            < 50_000f => "destitute",
            < 100_000f => "struggling",
            < 200_000f => "modest",
            < 300_000f => "prosperous",
            < 400_000f => "rich",
            < 600_000f => "luxurious",
            < 1_000_000f => "extravagant",
            < 1_500_000f => "opulent",
            < 2_000_000f => "glitterworld-tier",
            _ => "legendary"
        };
    }

    private static readonly ThoughtDef NeedBeautyThoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("NeedBeauty");

    /// <summary>
    /// The pawn's own beauty need, as vanilla's "ugly/beautiful environment" mood thought reads it,
    /// rather than an average of a few cells in front of the pawn.
    /// </summary>
    public static string Beauty(Pawn pawn)
    {
        var need = pawn?.needs?.TryGetNeed<Need_Beauty>();
        if (need == null) return null;

        // Mirrors ThoughtWorker_NeedBeauty's category-to-stage mapping; Neutral has no stage.
        int? stageIndex = need.CurCategory switch
        {
            BeautyCategory.Hideous => 0,
            BeautyCategory.VeryUgly => 1,
            BeautyCategory.Ugly => 2,
            BeautyCategory.Pretty => 3,
            BeautyCategory.VeryPretty => 4,
            BeautyCategory.Beautiful => 5,
            _ => null
        };

        if (stageIndex == null)
            return "unremarkable";

        // untranslatedLabel is the stage text before DefInjection: English whatever the game
        // language, while still following a mod that changes the stages.
        var stages = NeedBeautyThoughtDef?.stages;
        return stages != null && stageIndex.Value < stages.Count ? stages[stageIndex.Value].untranslatedLabel : null;
    }

    /// <summary>Vanilla's own cleanliness score stages, in English.</summary>
    public static string Cleanliness(float cleanliness)
    {
        return RoomStatDefOf.Cleanliness.GetScoreStage(cleanliness)?.untranslatedLabel;
    }

    public static string Resistance(float value)
    {
        return value switch
        {
            <= 0f => "broken",
            < 2f => "wavering",
            < 6f => "weakened",
            < 12f => "stubborn",
            _ => "defiant"
        };
    }

    public static string Will(float value)
    {
        return value switch
        {
            <= 0f => "broken",
            < 2f => "frail",
            < 6f => "moderate",
            < 12f => "resolute",
            _ => "unyielding"
        };
    }

    public static string Suppression(float value)
    {
        return value switch
        {
            < 20f => "rebellious",
            < 50f => "unruly",
            < 80f => "obedient",
            _ => "subdued"
        };
    }

    /// <summary>A standalone adjective, appended after a "Name(Relation)" label.</summary>
    public static string Opinion(float value)
    {
        return value switch
        {
            >= 80f => "adoring",
            >= 40f => "warm",
            >= 20f => "friendly",
            > -20f => "neutral",
            >= -40f => "cold",
            >= -80f => "hostile",
            _ => "loathing"
        };
    }

    /// <summary>Remaining hit points of a thing, as a word.</summary>
    public static string Condition(float pctHitPoints)
    {
        return pctHitPoints switch
        {
            >= 95f => "pristine",
            >= 75f => "scratched",
            >= 50f => "damaged",
            >= 25f => "badly damaged",
            _ => "wrecked"
        };
    }

    public static string GetLabelShort(this Gender gender)
    {
        return gender switch
        {
            Gender.Male => "M",
            Gender.Female => "F",
            _ => ""
        };
    }
}
