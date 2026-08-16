using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Patch;

[HarmonyPatch(typeof(HealthCardUtility), "EntryClicked")]
public static class HealthCardUtilityPatch
{
    public static bool Prefix(IEnumerable<Hediff> diffs, Pawn pawn)
    {
        if (diffs == null || diffs.All(h => h.def != Constant.VocalLinkDef)) return true;
        if (pawn == null) return false;
        Find.WindowStack.Add(new PersonaEditorWindow(pawn));
                
        if (Event.current != null) Event.current.Use();

        return false;
    }
}