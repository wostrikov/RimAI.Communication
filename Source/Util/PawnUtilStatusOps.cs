using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Ustas.RimAI.Communication.Data;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Ustas.RimAI.Communication.Util
{
    /// <summary>
    /// Pawn status formatting helpers for nearby contextual lines.
    /// </summary>
    internal static class PawnUtilStatusOps
    {
    internal static HashSet<Pawn> CollectRelevantPawns(Pawn mainPawn, List<Pawn> nearbyPawns)
    {
        var relevantPawns = new HashSet<Pawn> { mainPawn };

        if (mainPawn.CurJob != null)
            PawnUtil.AddJobTargetsToRelevantPawns(mainPawn.CurJob, relevantPawns);

        if (nearbyPawns != null)
        {
            relevantPawns.UnionWith(nearbyPawns);

            foreach (var nearby in nearbyPawns.Where(p => p.CurJob != null))
                PawnUtil.AddJobTargetsToRelevantPawns(nearby.CurJob, relevantPawns);
        }

        return relevantPawns;
    }

    internal static string GetPawnLabel(Pawn pawn, HashSet<Pawn> relevantPawns, bool useOptimization)
    {
        if (useOptimization)
            return pawn.LabelShort;

        return relevantPawns.Contains(pawn)
            ? ContextHelper.GetDecoratedName(pawn)
            : pawn.LabelShort;
    }

    internal static string GetPawnActivity(Pawn pawn, HashSet<Pawn> relevantPawns, bool useOptimization)
    {
        string activity = pawn.GetActivity();

        if (useOptimization || string.IsNullOrEmpty(activity))
            return activity;

        return DecorateText(activity, relevantPawns);
    }

    internal static void AddContextualInfo(Pawn pawn, List<string> lines, ref bool isInDanger)
    {
        if (pawn.IsVisitor())
        {
            lines.Add("Visiting user colony");
            return;
        }

        if (pawn.IsFreeColonist && pawn.GetMapRole() == MapRole.Invading)
        {
            lines.Add("You are away from colony, attacking to capture enemy settlement");
            return;
        }

        if (pawn.IsEnemy())
        {
            if (pawn.GetMapRole() == MapRole.Invading)
            {
                var lord = pawn.GetLord()?.LordJob;
                if (lord is LordJob_StageThenAttack || lord is LordJob_Siege)
                    lines.Add("waiting to invade user colony");
                else
                    lines.Add("invading user colony");
            }
            else
            {
                lines.Add("Fighting to protect your home from being captured");
            }

            return;
        }

        // Check for nearby hostiles
        Pawn nearestHostile = pawn.GetHostilePawnNearBy();
        if (nearestHostile != null)
        {
            float distance = pawn.Position.DistanceTo(nearestHostile.Position);

            if (distance <= 10f)
                lines.Add("Threat: Engaging in battle!");
            else if (distance <= 20f)
                lines.Add("Threat: Hostiles are dangerously close!");
            else
                lines.Add("Alert: hostiles in the area");

            isInDanger = true;
        }
    }

    /// <summary>
    /// Decorates text by replacing pawn names with their decorated versions
    /// </summary>
    internal static string DecorateText(string text, HashSet<Pawn> relevantPawns)
    {
        if (string.IsNullOrEmpty(text) || relevantPawns == null || !relevantPawns.Any())
            return text;

        // Build replacement map
        var replacements = relevantPawns
            .Select(p => new { Key = p.LabelShort, Value = ContextHelper.GetDecoratedName(p) })
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .OrderByDescending(x => x.Key.Length) // Longer names first to avoid partial matches
            .ToList();

        // Apply replacements
        return replacements.Aggregate(text, (current, replacement) =>
            current.Replace(replacement.Key, replacement.Value));
    }
    }
}
