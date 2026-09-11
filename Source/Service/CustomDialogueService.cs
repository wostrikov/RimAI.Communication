using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.UI;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

public static class CustomDialogueService
{
    private const float TalkDistance = 20f;
    public static readonly Dictionary<Pawn, PendingDialogue> PendingDialogues = new();

    public static void Tick()
    {
        List<Pawn> toRemove = [];

        foreach (var (initiator, dialogue) in PendingDialogues)
        {
            // Check if pawn is still valid
            if (initiator == null || initiator.Destroyed || dialogue.Recipient == null || dialogue.Recipient.Destroyed)
            {
                toRemove.Add(initiator);
                continue;
            }

            if (!CanTalk(initiator, dialogue.Recipient)) continue;

            ExecuteDialogue(initiator, dialogue.Recipient, dialogue.Message, dialogue.IsAnnouncement);
            toRemove.Add(initiator);
        }

        foreach (Pawn pawn in toRemove)
        {
            PendingDialogues.Remove(pawn);
        }
    }

    private static bool InSameRoom(Pawn pawn1, Pawn pawn2)
    {
        Room room1 = pawn1.GetRoom();
        Room room2 = pawn2.GetRoom();
        return (room1 != null && room2 != null && room1 == room2) ||
               (room1 == null && room2 == null); // Both outdoors
    }

    public static bool CanTalk(Pawn initiator, Pawn recipient)
    {
        if (initiator == null || recipient == null) return false;

        // Player talking to a pawn is always allowed
        if (initiator.IsPlayer()) return true;

        float distance = initiator.Position.DistanceTo(recipient.Position);
        return distance <= TalkDistance && InSameRoom(initiator, recipient);
    }

    public static void ExecuteDialogue(Pawn initiator, Pawn recipient, string message, bool isAnnouncement = false)
    {
        PawnState initiatorState = Cache.Get(initiator);
        if (initiatorState == null || !initiatorState.CanDisplayTalk())
            return;

        TalkType talkType = isAnnouncement ? TalkType.Announcement : TalkType.User;

        if (isAnnouncement)
        {
            // An announcement is spoken by the pawn - by the player through it, or by the pawn
            // itself - and everyone in earshot reacts; nobody in particular is being answered.
            Pawn speaker = initiator.IsPlayer() ? recipient : initiator;
            Pawn other = initiator.IsPlayer() ? initiator : recipient;

            PawnState speakerState = Cache.Get(speaker);
            if (speakerState != null && speakerState.CanDisplayTalk())
            {
                speakerState.TalkRequests.AddFirst(new TalkRequest(message, speaker, other, talkType));
                speakerState.IgnoreAllTalkResponses();
                UserRequestPool.Add(speaker);
            }
        }
        else
        {
            PawnState recipientState = Cache.Get(recipient);
            if (recipientState != null && recipientState.CanDisplayTalk())
                recipientState.AddTalkRequest(message, initiator, talkType);
        }

        // The player's own line never waits behind a background talk.
        if (AIService.IsBusy())
            AIService.CancelCurrent();

        ApiLog apiLog = ApiHistory.AddUserHistory(initiator, recipient, message, talkType);

        if (initiator.IsPlayer())
        {
            apiLog.SpokenTick = GenTicks.TicksGame;
            Overlay.NotifyLogUpdated();
        }
        else
        {
            TalkResponse talkResponse = new(talkType, initiator.LabelShort, message)
            {
                Id = apiLog.Id
            };
            Cache.Get(initiator).TalkResponses.Insert(0, talkResponse);
        }

        TalkLifecycle.PublishPlayerDialogueSubmitted(initiator, recipient, message);
    }

    public class PendingDialogue(Pawn recipient, string message, bool isAnnouncement = false)
    {
        public readonly Pawn Recipient = recipient;
        public readonly string Message = message;
        public readonly bool IsAnnouncement = isAnnouncement;
    }
}
