using System.Collections.Concurrent;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Communication.Data;

public class PawnState(Pawn pawn)
{
    public readonly Pawn Pawn = pawn;
    public string Context { get; set; }
    public int LastTalkTick { get; set; } = 0;
    public string LastStatus { get; set; } = "";
    public int RejectCount { get; set; }
    public readonly List<TalkResponse> TalkResponses = [];
    private readonly ConcurrentQueue<TalkResponse> _incomingTalkResponses = new();
    // Same hazard as AIService._busy: set on the main thread, cleared in a finally on a
    // threadpool thread. An auto-property gives no memory barrier, and a stale `true` here
    // silences this pawn for good.
    private volatile bool _isGeneratingTalk;
    public bool IsGeneratingTalk
    {
        get => _isGeneratingTalk;
        set => _isGeneratingTalk = value;
    }
    public readonly LinkedList<TalkRequest> TalkRequests = [];
    
    public HashSet<Hediff> Hediffs { get; set; } = pawn.GetHediffs();

    public string Personality => PersonaService.GetPersonality(Pawn);
    public double TalkInitiationWeight => PersonaService.GetTalkInitiationWeight(Pawn);

    public void AddTalkRequest(string prompt, Pawn recipient = null, TalkType talkType = TalkType.Other,
        SleepDialogueKind sleepDialogueKind = SleepDialogueKind.None)
    {
        // 1. If Urgent, clear out less important active requests
        if (talkType == TalkType.Urgent)
        {
            var currentNode = TalkRequests.First;
            while (currentNode != null)
            {
                var nextNode = currentNode.Next;
                var request = currentNode.Value;
                
                // If we overwrite a request, send it to global history as expired/overwritten
                if (!request.TalkType.IsFromUser())
                {
                    TalkRequestPool.AddToHistory(request, RequestStatus.Expired);
                    TalkRequests.Remove(currentNode);
                }
                currentNode = nextNode;
            }
        }

        // 2. Create and Enqueue
        var newRequest = new TalkRequest(prompt, Pawn, recipient, talkType)
        {
            Status = RequestStatus.Pending,
            SleepDialogueKind = sleepDialogueKind
        };

        if (talkType.IsFromUser())
        {
            TalkRequests.AddFirst(newRequest);
            IgnoreAllTalkResponses();
            Cache.Get(recipient)?.IgnoreAllTalkResponses();
            UserRequestPool.Add(Pawn);
        }
        else if (talkType == TalkType.Interaction)
        {
            TalkRequests.AddFirst(newRequest);
            IgnoreAllTalkResponses();
            Cache.Get(recipient)?.IgnoreAllTalkResponses();
            LastTalkTick = 0;
            UserRequestPool.Add(Pawn);
        }
        else if (talkType == TalkType.Sleep)
        {
            // A bedtime or waking line belongs to that moment: first in line, and taken
            // through the request pool rather than waiting for the pawn's next turn to talk.
            newRequest.IsMonologue = recipient == null;
            TalkRequests.AddFirst(newRequest);
            LastTalkTick = 0;
            UserRequestPool.Add(Pawn);
        }
        else if (talkType is TalkType.Event or TalkType.QuestOffer)
        {
            TalkRequests.AddFirst(newRequest);
        }
        else
        {
            TalkRequests.AddLast(newRequest);   
        }
    }
    
    public TalkRequest GetNextTalkRequest()
    {
        var node = TalkRequests.First;
        while (node != null)
        {
            var request = node.Value;
            var next = node.Next;
        
            if (!request.IsExpired())
                return request;
            
            TalkRequestPool.AddToHistory(request, RequestStatus.Expired);
            TalkRequests.Remove(node);
            node = next;
        }
        return null;
    }

    public void MarkRequestSpoken(TalkRequest request)
    {
        TalkRequestPool.AddToHistory(request, RequestStatus.Processed);
        TalkRequests.Remove(request);
    }

    /// <summary>
    /// Thread-safe hand-off for background threads to submit a talk response. Call
    /// <see cref="DrainIncomingTalkResponses"/> from the main thread before reading
    /// <see cref="TalkResponses"/> to move queued entries in.
    /// </summary>
    public void QueueIncomingResponse(TalkResponse talkResponse)
    {
        if (!TalkLifecycle.CanEnqueueTalkResponse(this, talkResponse))
            return;
        _incomingTalkResponses.Enqueue(talkResponse);
    }

    /// <summary>
    /// Moves any responses queued from background threads into <see cref="TalkResponses"/>.
    /// Must only be called from the main thread.
    /// </summary>
    public void DrainIncomingTalkResponses()
    {
        while (_incomingTalkResponses.TryDequeue(out var talkResponse))
        {
            TalkResponses.Add(talkResponse);
            TalkLifecycle.PublishTalkResponseQueued(Pawn, talkResponse);
        }
    }

    public bool CanDisplayTalk()
    {
        if (Pawn.IsPlayer()) return true;
        
        // Where the camera is looking decides whether new talk is generated, not whether talk
        // already generated may be shown - otherwise glancing at the world map or another map
        // throws every pending line away.
        if (Pawn.Map == null || !Pawn.Spawned)
            return false;
        
        CommunicationSettings settings = Settings.Get();
        if (!settings.DisplayTalkWhenDrafted && Pawn.Drafted) return false;
        if (!settings.ContinueDialogueWhileSleeping && !Pawn.Awake()) return false;

        return !Pawn.Dead && TalkInitiationWeight > 0;
    }

    public bool CanGenerateTalk()
    {
        if (Pawn.IsPlayer()) return true;
        if (WorldRendererUtility.CurrentWorldRenderMode == WorldRenderMode.Planet || Find.CurrentMap == null ||
            Pawn.Map != Find.CurrentMap)
            return false;
        DrainIncomingTalkResponses();
        return !IsGeneratingTalk && CanDisplayTalk() && Pawn.Awake() && TalkResponses.Empty()
               && CommonUtil.HasPassed(LastTalkTick, CommunicationSettings.ReplyInterval);
    }

    public void IgnoreTalkResponse()
    {
        if (TalkResponses.Count == 0) return;
        var talkResponse = TalkResponses[0];
        TalkHistory.AddIgnored(talkResponse.Id);
        TalkResponses.Remove(talkResponse);

        var log = ApiHistory.GetApiLog(talkResponse.Id);
        if (log != null) log.SpokenTick = -1;
    }

    public void IgnoreAllTalkResponses(List<TalkType> keepTypes = null)
    {
        DrainIncomingTalkResponses();
        if (keepTypes == null)
            while (TalkResponses.Count > 0)
                IgnoreTalkResponse();
        else
            TalkResponses.RemoveAll(response =>
            {
                if (keepTypes.Contains(response.TalkType)) return false;
                TalkHistory.AddIgnored(response.Id);
                return true;
            });
    }
}