using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Util;

/// <summary>
/// What a pawn is going through right now, read off its health and its job, for choosing the tone
/// of what it says. Being in danger is <see cref="PawnUtil.IsInDanger"/>'s question, not this one.
/// </summary>
public static class PawnCondition
{
    /// <summary>Pain at which a downed pawn is suffering rather than just lying there.</summary>
    private const float SeverePain = 0.4f;

    /// <summary>
    /// Downed and hurting. Downed alone says nothing: a baby is downed all its life, and so is a
    /// pawn who cannot walk.
    /// </summary>
    public static bool IsDownedInPain(this Pawn pawn)
    {
        if (pawn is not { Downed: true } || pawn.health?.hediffSet == null) return false;
        return pawn.health.InPainShock || pawn.health.hediffSet.PainTotal >= SeverePain;
    }

    /// <summary>In a social fight, or swinging at someone of its own faction.</summary>
    public static bool IsBrawlingWithAlly(this Pawn pawn, out Pawn opponent)
    {
        opponent = null;
        var job = pawn?.CurJob;
        if (job == null) return false;

        if (job.def == JobDefOf.SocialFight)
        {
            opponent = job.targetA.Thing as Pawn;
            return true;
        }

        if ((job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic)
            && job.targetA.Thing is Pawn target && target.RaceProps?.Humanlike == true
            && target.Faction != null && target.Faction == pawn.Faction)
        {
            opponent = target;
            return true;
        }

        return false;
    }

    /// <summary>Running away from the fight.</summary>
    public static bool IsFleeing(this Pawn pawn) =>
        pawn?.CurJobDef == JobDefOf.Flee || pawn?.CurJobDef == JobDefOf.FleeAndCower;

    /// <summary>
    /// Tending, feeding, rescuing, capturing or escorting someone - a duty that brings the two
    /// together whatever they think of each other.
    /// </summary>
    public static bool IsCaringFor(this Pawn pawn, out Pawn patient)
    {
        patient = null;
        var job = pawn?.CurJob;
        if (job?.def == null) return false;

        var def = job.def;
        bool isCare = def == JobDefOf.Rescue || def == JobDefOf.TendPatient || def == JobDefOf.FeedPatient
                      || def == JobDefOf.Capture || def == JobDefOf.Arrest
                      || def == JobDefOf.TakeWoundedPrisonerToBed || def == JobDefOf.EscortPrisonerToBed;
        if (!isCare) return false;

        patient = job.targetA.Thing as Pawn;
        return patient != null && patient != pawn;
    }
}
