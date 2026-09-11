using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Keeps the colony's open story threads: opens one when a pawn tells a story it was asked to
/// leave unfinished, and hands it later to someone who heard it, to carry on or to end.
/// </summary>
public static class StoryThreadService
{
    private static readonly ConcurrentQueue<(TalkRequest request, List<TalkResponse> responses)> Pending = new();
    private static readonly Random Rng = new();

    private static List<StoryThread> Threads => Find.World?.GetComponent<RimTalkWorldComponent>()?.StoryThreads;

    /// <summary>Sometimes, an open thread this pawn heard and did not tell last.</summary>
    public static bool TryPickContinuation(Pawn mainPawn, out StoryThread thread)
    {
        thread = null;
        var threads = Threads;
        if (threads == null || mainPawn == null) return false;

        int now = GenTicks.TicksGame;
        threads.RemoveAll(t => t.IsClosed || StoryThreadPolicy.IsExpired(t.LastTick, now));
        if (threads.Count == 0 || Rng.NextDouble() >= StoryThreadPolicy.ContinueChance) return false;

        string id = mainPawn.ThingID;
        thread = threads
            .Where(t => t.HeardBy.Contains(id) && t.LastTellerId != id)
            .OrderBy(t => t.LastTick)
            .FirstOrDefault();
        return thread != null;
    }

    /// <summary>Called on the background thread once a talk is complete; applied on the main thread.</summary>
    public static void Capture(TalkRequest request, List<TalkResponse> responses)
    {
        if (request == null || responses == null || responses.Count == 0) return;
        if (request.StoryThreadId == null && request.StoryOpening == null) return;
        Pending.Enqueue((request, responses.ToList()));
    }

    /// <summary>Main thread only.</summary>
    public static void DrainCaptures()
    {
        while (Pending.TryDequeue(out var item))
            Apply(item.request, item.responses);
    }

    private static void Apply(TalkRequest request, List<TalkResponse> responses)
    {
        var threads = Threads;
        if (threads == null) return;

        string told = StoryThreadPolicy.Summarize(responses.Select(r => $"{r.Name}: {r.Text}"));
        if (string.IsNullOrEmpty(told)) return;

        var heard = new HashSet<string>();
        foreach (var pawn in (request.Participants ?? []).Append(request.Initiator).Append(request.Recipient))
            if (pawn != null) heard.Add(pawn.ThingID);
        foreach (var response in responses)
            if (Cache.GetByName(response.Name)?.Pawn is { } speaker) heard.Add(speaker.ThingID);

        int now = GenTicks.TicksGame;
        string tellerId = request.Initiator?.ThingID;

        if (request.StoryThreadId != null)
        {
            var thread = threads.FirstOrDefault(t => t.Id == request.StoryThreadId);
            if (thread == null) return;
            thread.Summary = StoryThreadPolicy.Extend(thread.Summary, told);
            thread.Steps++;
            thread.LastTick = now;
            thread.LastTellerId = tellerId;
            foreach (var id in heard)
                if (!thread.HeardBy.Contains(id)) thread.HeardBy.Add(id);
            thread.IsClosed = StoryThreadPolicy.ShouldClose(thread.Steps);
            return;
        }

        threads.Add(new StoryThread
        {
            Id = Guid.NewGuid().ToString("N"),
            Teller = request.Initiator?.LabelShort ?? "someone",
            Subject = request.StoryOpening,
            Summary = told,
            HeardBy = heard.ToList(),
            LastTellerId = tellerId,
            Steps = 1,
            CreatedTick = now,
            LastTick = now
        });

        // Only a few at a time: the oldest quiet one gives way.
        while (threads.Count(t => !t.IsClosed) > StoryThreadPolicy.MaxOpenThreads)
            threads.Remove(threads.Where(t => !t.IsClosed).OrderBy(t => t.LastTick).First());
    }
}
