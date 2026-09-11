using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// The rules of a story thread: a story one pawn tells that others later carry on - what
/// happened next, a twist, a rumor about it - until someone tells how it ended.
/// </summary>
public static class StoryThreadPolicy
{
    public const int MaxOpenThreads = 5;
    public const int MaxSteps = 4;                      // the telling and up to three more
    public const int ExpireAfterTicks = 60000 * 10;    // ten in-game days without a word
    public const double ContinueChance = 0.35;
    public const int MaxSummaryChars = 600;

    private static readonly string[] StoryApproaches = ["storytelling", "rumor", "confession", "speculation"];
    private static readonly string[] StorySubjectMarks = ["legend", "rumor", "myth", "stories", "tales"];

    /// <summary>Whether a topic asks for a story someone could carry on later.</summary>
    public static bool OpensThread(string approach, string subject)
    {
        if (!string.IsNullOrEmpty(approach) && StoryApproaches.Contains(approach, StringComparer.OrdinalIgnoreCase))
            return true;
        return !string.IsNullOrEmpty(subject) &&
               StorySubjectMarks.Any(mark => subject.IndexOf(mark, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static string OpeningInstruction(string approach, string subject)
    {
        return $"Tell it as a short story ({approach}, {subject}) and leave something unexplained that could be picked up later.";
    }

    /// <summary>What the next teller is asked for: more of it, a twist, and at the last step an ending.</summary>
    public static string ContinuationInstruction(int stepsSoFar, string teller, string subject, string summary)
    {
        string ask = stepsSoFar + 1 >= MaxSteps
            ? "bring it to an ending: say how it finally turned out"
            : stepsSoFar % 2 == 1
                ? "tell what happened next, or a new detail someone heard"
                : "add a twist, a doubt, or a fresh rumor about it";
        return $"Story thread: {teller} earlier told a story about {subject}: \"{summary}\". " +
               $"Pick it up - {ask}. Keep it consistent with what was already said.";
    }

    /// <summary>The spoken lines as one short passage, cut to fit.</summary>
    public static string Summarize(IEnumerable<string> lines, int maxChars = MaxSummaryChars)
    {
        string text = string.Join(" / ", (lines ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));
        return text.Length <= maxChars ? text : text.Substring(0, maxChars - 1).TrimEnd() + "…";
    }

    /// <summary>The story so far with a new part added; when too long, the oldest part goes first.</summary>
    public static string Extend(string summary, string addition, int maxChars = MaxSummaryChars)
    {
        if (string.IsNullOrWhiteSpace(summary)) return Summarize([addition], maxChars);
        if (string.IsNullOrWhiteSpace(addition)) return summary;
        string text = $"{summary} → {addition}";
        return text.Length <= maxChars ? text : "…" + text.Substring(text.Length - (maxChars - 1)).TrimStart();
    }

    public static bool IsExpired(int lastTick, int now) => now - lastTick > ExpireAfterTicks;

    public static bool ShouldClose(int steps) => steps >= MaxSteps;
}
