using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Util;

/// <summary>
/// The signals behind <see cref="PawnUtil.IsInCombat"/>: a remembered target that is still a
/// threat, and a verb aimed at someone hostile. Kept apart from PawnUtil, whose public methods
/// are all template functions.
/// </summary>
internal static class PawnCombat
{
    internal static bool IsLiveThreat(Pawn pawn, Thing target)
    {
        if (target == null || target.Destroyed || !target.Spawned) return false;
        if (target.Map != pawn.Map) return false;
        if (target is Pawn targetPawn && (targetPawn.Dead || targetPawn.Downed)) return false;
        // A captured raider, a tamed animal or a berserker who came round stays the remembered
        // target, but is no threat any more.
        if (!IsHostilePair(target, pawn)) return false;
        return pawn.Position.DistanceTo(target.Position) <= 30f;
    }

    /// <summary>
    /// A verb in Stance_Busy is also a hunter aiming at a deer or a psycaster casting on a
    /// friend, and those pawns talked as if under fire. Only a verb aimed at something hostile
    /// counts; a caster in a real fight is still caught by the other threat checks.
    /// </summary>
    internal static bool IsFiringAtHostile(Pawn pawn)
    {
        if (pawn.stances?.curStance is not Stance_Busy { verb: not null } busy) return false;
        if (busy.verb is Verb_CastAbility) return false;
        if (pawn.CurJobDef == JobDefOf.Hunt) return false;
        return busy.focusTarg.Thing is { } focus && IsHostilePair(focus, pawn);
    }

    /// <summary>
    /// HostileTo, without RimWorld's "dummy relation" error: a hidden faction may have no
    /// relation with the other side at all, and HostileTo asks for one.
    /// </summary>
    internal static bool IsHostilePair(Thing target, Pawn pawn)
    {
        Faction a = target.Faction;
        Faction b = pawn.Faction;
        if (a != null && b != null && a != b && (a.def.hidden || b.def.hidden)
            && a.RelationWith(b, allowNull: true) == null)
        {
            return a.def.permanentEnemy || b.def.permanentEnemy
                || (target as Pawn)?.InAggroMentalState == true || pawn.InAggroMentalState;
        }

        return target.HostileTo(pawn);
    }
}
