using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Communication.Service;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Communication.Patches;

/// <summary>Watches job changes for the moment a pawn lies down to sleep and the moment it gets up.</summary>
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
public static class SleepPatch
{
    public static void Prefix(Pawn ___pawn, out (JobDef prevJob, bool wasAsleep) __state)
    {
        JobDef previousJob = ___pawn?.CurJobDef;
        bool wasAsleep = ___pawn != null && SleepDialogueTracker.IsSleepJob(previousJob) && !___pawn.Awake();
        __state = (previousJob, wasAsleep);
    }

    public static void Postfix(Pawn ___pawn, Job newJob, (JobDef prevJob, bool wasAsleep) __state)
    {
        if (___pawn == null || newJob == null) return;

        // Some relationship jobs end LayDown before starting their own, so leaving the bed
        // for one of those is not waking up.
        if (SleepDialogueTracker.IsBedtimeInterruptionJob(newJob.def))
        {
            SleepDialogueTracker.Notify_SleepInterrupted(___pawn);
            return;
        }

        bool prevWasSleep = SleepDialogueTracker.IsSleepJob(__state.prevJob);
        bool currIsSleep = SleepDialogueTracker.IsSleepJob(newJob.def);

        if (!prevWasSleep && currIsSleep)
            SleepDialogueTracker.Notify_GoingToBed(___pawn);
        else if (prevWasSleep && !currIsSleep)
            SleepDialogueTracker.Notify_WokeUp(___pawn, __state.wasAsleep);
    }
}
