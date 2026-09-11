using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Util;
using UnityEngine;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Patches
{
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class PawnGizmoPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null) return;
            if (!Settings.Get().AllowCustomConversation) return;
            if (Settings.Get().PlayerDialogueMode == Settings.PlayerDialogueMode.Disabled) return;
            if (!__instance.Spawned || __instance.Dead) return;
            if (!__instance.IsTalkEligible()) return;
            if (__instance.IsPlayer()) return;

            var selector = Find.Selector;
            if (selector.SelectedPawns.Count != 1) return;

            var list = (__result != null) ? __result.ToList() : new List<Gizmo>();

            // The player talks to this pawn.
            list.Add(new Command_Action
            {
                defaultLabel = "RimTalk.Gizmo.ChatWithTarget".Translate(__instance.LabelShort),
                defaultDesc = "RimTalk.Gizmo.ChatWithTargetDesc".Translate(__instance.LabelShort),
                icon = ContentFinder<Texture2D>.Get("UI/ChatGizmo", true),
                action = () =>
                {
                    Pawn player = Cache.GetPlayer();
                    if (player == null) return;
                    Find.WindowStack.Add(new CustomDialogueWindow(player, __instance, DialogueMode.Direct));
                }
            });

            // This pawn announces something to everyone in earshot; the player is not in it.
            if (Settings.Get().AllowAnnouncement)
            {
                list.Add(new Command_Action
                {
                    defaultLabel = "RimTalk.Gizmo.Announce".Translate(),
                    defaultDesc = "RimTalk.Gizmo.AnnounceDesc".Translate(__instance.LabelShort),
                    icon = ContentFinder<Texture2D>.Get("UI/AnnounceGizmo", true),
                    action = () => Find.WindowStack.Add(new CustomDialogueWindow(__instance, __instance, DialogueMode.Announce))
                });
            }

            __result = list;
        }
    }
}
