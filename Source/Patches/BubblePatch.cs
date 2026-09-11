using System;
using System.Linq;
using Bubbles.Core;
using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Communication.Util;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Communication.Patches;

[HarmonyPatch(typeof(Bubbler), nameof(Bubbler.Add))]
public static class Bubbler_Add
{
    private static bool _originalDraftedValue;

    public static bool Prefix(LogEntry entry)
    {
        CommunicationSettings settings = Settings.Get();

        if (IsRimTalkInteraction(entry))
        {
            if (settings.DisplayTalkWhenDrafted)
                try
                {
                    _originalDraftedValue = Bubbles.Settings.DoDrafted.Value;
                    Bubbles.Settings.DoDrafted.Value = true;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to override bubble drafted setting: {ex.Message}");
                }

            return true;
        }

        if (!settings.IsEnabled || !settings.ProcessNonRimTalkInteractions)
        {
            return true;
        }

        Pawn[] pawns = entry.GetConcerns().OfType<Pawn>().Take(2).ToArray();
        if (pawns.Length < 2
            || pawns[0].RaceProps?.Humanlike != true
            || pawns[1].RaceProps?.Humanlike != true)
        {
            // Bubbles also forwards animal interactions. Rendering their log
            // entry from a human POV asks Verse for grammar symbols the animal
            // interaction does not supply (for example INITIATOR_nameDef).
            return true;
        }

        Pawn initiator = pawns[0];
        Pawn recipient = pawns[1];
            
        InteractionDef interactionDef = GetInteractionDef(entry);
        if (interactionDef == null) return true;
        string prompt = entry.ToGameStringFromPOV(initiator).StripTags();
        bool isFastTrack = settings.IsFastTrackInteraction(interactionDef.defName);
        bool isChitchat = interactionDef == InteractionDefOf.Chitchat ||
                          interactionDef == InteractionDefOf.DeepTalk;

        if (!isFastTrack && isChitchat
            && (initiator.IsInDanger()
                || initiator.GetHostilePawnNearBy() != null
                || !PawnSelector.GetNearByTalkablePawns(initiator).Contains(recipient)))
        {
            return false;
        }

        PawnState pawnState = Cache.Get(initiator);

        // chitchat is ignored if talkRequest exists
        if (pawnState == null || (!isFastTrack && isChitchat && pawnState.TalkRequests.Count > 0))
            return false;

        // A fast-track line waits for nobody, but it does not talk over a pawn already speaking.
        if (isFastTrack)
        {
            pawnState.DrainIncomingTalkResponses();
            if (pawnState.IsGeneratingTalk || pawnState.TalkResponses.Count > 0)
                return false;

            PawnState recipientState = Cache.Get(recipient);
            recipientState?.DrainIncomingTalkResponses();
            if (recipientState != null && (recipientState.IsGeneratingTalk || recipientState.TalkResponses.Count > 0))
                return false;
        }

        // Otherwise, block normal bubble and generate talk
        prompt = $"{prompt} ({interactionDef.label})";
        pawnState.AddTalkRequest(prompt, recipient, isFastTrack ? TalkType.Interaction : TalkType.Chitchat);
        return false;
    }

    public static void Postfix()
    {
        // Roll back original bubble settings for drafted
        if (Settings.Get().DisplayTalkWhenDrafted)
        {
            try
            {
                Bubbles.Settings.DoDrafted.Value = _originalDraftedValue;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to restore bubble drafted setting: {ex.Message}");
            }
        }
    }

    private static bool IsRimTalkInteraction(LogEntry entry)
    {
        return entry is PlayLogEntry_RimTalkInteraction ||
               (entry is PlayLogEntry_Interaction interaction &&
                InteractionTextPatch.IsRimTalkInteraction(interaction));
    }

    private static InteractionDef GetInteractionDef(LogEntry entry)
    {
        var field = AccessTools.Field(entry.GetType(), "intDef");
        return field?.GetValue(entry) as InteractionDef;
    }
}
