using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Patches;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Data;

public enum RequestStatus
{
    Pending,
    Processed,
    Expired
}

public enum SleepDialogueKind
{
    None,
    Bedtime,
    WakeUp
}

public class TalkRequest(string prompt, Pawn initiator, Pawn recipient = null, TalkType talkType = TalkType.Other)
{
    public SleepDialogueKind SleepDialogueKind { get; set; }

    /// <summary>
    /// The topic, story opening or story continuation this talk was given - drawn once, since the
    /// dialogue type is built twice per talk and a second draw would hand each a different one.
    /// </summary>
    public string TopicHint { get; set; }
    public bool TopicHintDrawn { get; set; }

    /// <summary>The story thread this talk continues, if any.</summary>
    public string StoryThreadId { get; set; }

    /// <summary>The subject of a story this talk was asked to open, if any.</summary>
    public string StoryOpening { get; set; }
    public TalkType TalkType { get; set; } = talkType;
    public string Context { get; set; }
    public string Prompt { get; set; } = prompt;
    public string RawPrompt { get; set; } = prompt;
    public Pawn Initiator { get; set; } = initiator;
    public Pawn Recipient { get; set; } = recipient;
    public int MapId { get; set; }
    public int CreatedTick { get; set; } = GenTicks.TicksGame;
    public DateTime CreatedTime { get; set; } = DateTime.Now; 
    public int FinishedTick { get; set; } = -1; 
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public bool IsMonologue;
    public bool IsAnnouncement => TalkType == TalkType.Announcement;
    
    /// <summary>
    /// All pawns participating in the dialogue (filled in sync layer)
    /// </summary>
    public List<Pawn> Participants { get; set; }
    
    /// <summary>
    /// Pre-built message list (built by PromptManager in sync layer)
    /// </summary>
    public List<(Role role, string content)> PromptMessages { get; set; }

    /// <summary>
    /// Pre-built prompt segments (built by PromptManager in sync layer)
    /// </summary>
    public List<PromptMessageSegment> PromptMessageSegments { get; set; }

    public bool IsExpired()
    {
        if (TalkType.IsFromUser()) return false;
        if (TalkType == TalkType.Sleep)
            return GenTicks.TicksGame - CreatedTick > 5000;
        int duration = 20;
        if (TalkType == TalkType.Urgent)
        {
            duration = 5;
            if (!Initiator.IsInDanger())
            {
                return true;
            }
        } else if (TalkType == TalkType.Thought)
        {
            return !ThoughtTracker.IsThoughtStillActive(Initiator, Prompt);
        }
        return GenTicks.TicksGame - CreatedTick > CommonUtil.GetTicksForDuration(duration);
    }
    
    public TalkRequest Clone()
    {
        return (TalkRequest) MemberwiseClone();
    }
}
