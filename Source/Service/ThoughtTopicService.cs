using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Policy;
using Ustas.RimAI.Communication.Util;
using RimWorld;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// A pawn's strongest feelings, for the context and as something to talk about. Main thread
/// only: it reads the pawn's thoughts, which the prompt is built from on the main thread.
/// </summary>
public static class ThoughtTopicService
{
    private static readonly Random Rng = new();

    /// <summary>The pawn's thoughts by the size of their mood effect, strongest first.</summary>
    public static List<(Thought thought, float offset)> GetStrongestThoughts(Pawn pawn, int count)
    {
        if (pawn?.needs?.mood?.thoughts == null) return [];
        var thoughts = ContextHelper.GetThoughts(pawn).Select(kvp => (thought: kvp.Key, offset: kvp.Value));
        return ThoughtTopicPolicy.Strongest(thoughts, t => t.offset, count);
    }

    /// <summary>"Label(+12), Label(-15)" for the strongest thoughts, or null when there are none.</summary>
    public static string DescribeStrongestThoughts(Pawn pawn, int count)
    {
        var strongest = GetStrongestThoughts(pawn, count);
        if (strongest.Count == 0) return null;
        return string.Join(", ", strongest.Select(t => ThoughtTopicPolicy.Format(LabelOf(t.thought), t.offset)));
    }

    /// <summary>
    /// Most of the time, a topic line that opens the talk on one of the speaker's strongest
    /// feelings and has the partner answer with their own. Null when this talk opens otherwise.
    /// </summary>
    public static string TryDrawTopic(Pawn speaker, Pawn partner, List<Pawn> pawns)
    {
        if (speaker == null || !speaker.RaceProps.Humanlike || !Settings.Get().Context.IncludeThoughts) return null;
        if (Rng.NextDouble() >= ThoughtTopicPolicy.ThoughtTopicChance) return null;

        var candidates = GetStrongestThoughts(speaker, ThoughtTopicPolicy.Candidates);
        var state = Cache.Get(speaker);
        int index = ThoughtTopicPolicy.Pick(
            candidates.Select(t => t.thought.def?.defName).ToList(),
            candidates.Select(t => t.offset).ToList(),
            state?.LastThoughtTopicDefName,
            Rng.NextDouble());
        if (index < 0) return null;

        var (thought, offset) = candidates[index];
        if (state != null) state.LastThoughtTopicDefName = thought.def?.defName;

        string partnerName = partner != null ? PromptService.GetUniqueName(partner, pawns) : null;
        string partnerThought = partner != null && partner.RaceProps.Humanlike ? DescribeStrongestThoughts(partner, 1) : null;
        return ThoughtTopicPolicy.Compose(PromptService.GetUniqueName(speaker, pawns), LabelOf(thought), offset,
            partnerName, partnerThought);
    }

    private static string LabelOf(Thought thought)
        => CommonUtil.Sanitize(thought.LabelCap ?? thought.def?.defName ?? "UnknownThought");
}
