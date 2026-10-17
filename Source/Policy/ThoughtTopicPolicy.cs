using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// The pawn's strongest feelings as talk material: which thoughts count as strongest, how one
/// reads in the prompt, and which one a pawn brings up. Free of the game so it can be tested;
/// the thoughts themselves are read by ThoughtTopicService on the main thread.
/// </summary>
public static class ThoughtTopicPolicy
{
    /// <summary>The share of talks without a prompt of their own that open on a feeling.</summary>
    public const double ThoughtTopicChance = 0.70;

    /// <summary>How many of the speaker's strongest thoughts the topic is drawn from.</summary>
    public const int Candidates = 3;

    /// <summary>Items ordered by the size of their mood effect, strongest first, at most <paramref name="count"/>.</summary>
    public static List<T> Strongest<T>(IEnumerable<T> items, Func<T, float> offset, int count)
    {
        if (items == null || count <= 0) return [];
        return items
            .Where(item => Rounded(offset(item)) != 0)
            .OrderByDescending(item => Math.Abs(offset(item)))
            .Take(count)
            .ToList();
    }

    /// <summary>"Label(+12)" or "Label(-15)": the feeling and how much it weighs.</summary>
    public static string Format(string label, float offset)
    {
        return $"{label}({Signed(offset)})";
    }

    /// <summary>
    /// The index of the thought to bring up: weighted by the size of each one's effect, skipping
    /// the one this pawn brought up last while there is another to choose. -1 when none.
    /// </summary>
    /// <param name="roll">A uniform draw in [0, 1).</param>
    public static int Pick(IReadOnlyList<string> defNames, IReadOnlyList<float> offsets, string lastDefName, double roll)
    {
        if (defNames == null || offsets == null || defNames.Count == 0 || defNames.Count != offsets.Count) return -1;

        double total = 0;
        for (int i = 0; i < offsets.Count; i++)
            if (defNames[i] != lastDefName)
                total += Math.Abs(offsets[i]);
        if (total <= 0) return -1;

        double target = roll * total;
        double cumulative = 0;
        int lastEligible = -1;
        for (int i = 0; i < offsets.Count; i++)
        {
            if (defNames[i] == lastDefName) continue;
            lastEligible = i;
            cumulative += Math.Abs(offsets[i]);
            if (target < cumulative) return i;
        }
        return lastEligible;
    }

    /// <summary>
    /// The topic line. With a partner, the speaker raises the feeling and the partner answers
    /// with one of their own; alone, the speaker turns it over in their head.
    /// </summary>
    public static string Compose(string speaker, string label, float offset, string partner, string partnerThought)
    {
        string thought = $"'{label}'({Signed(offset)})";
        if (string.IsNullOrEmpty(partner))
            return $"Topic: {speaker} mulls over {thought} under their breath";
        return string.IsNullOrEmpty(partnerThought)
            ? $"Topic: {speaker} brings up {thought} with {partner}; {partner} reacts from their own feelings"
            : $"Topic: {speaker} brings up {thought} with {partner}; {partner} reacts with their own ({partnerThought})";
    }

    private static string Signed(float offset)
    {
        int value = Rounded(offset);
        string digits = value.ToString(CultureInfo.InvariantCulture);
        return value > 0 ? "+" + digits : digits;
    }

    private static int Rounded(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
