using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Communication.Patches;

[HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
public static class MentalStatePatch
{
    public static void Postfix(Pawn ___pawn, MentalStateDef stateDef, bool __result)
    {
        if (__result && ___pawn != null && !ShouldSkipIgnore(stateDef))
        {
            Cache.Get(___pawn)?.IgnoreAllTalkResponses();
        }
    }

    private static bool ShouldSkipIgnore(MentalStateDef stateDef)
    {
        var stateClass = stateDef?.stateClass;
        return stateClass == typeof(MentalState_BabyGiggle) || stateClass == typeof(MentalState_BabyCry);
    }
}