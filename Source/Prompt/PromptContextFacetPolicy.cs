namespace Ustas.RimAI.Communication.Prompt;

/// <summary>
/// Authoritative markers for the six promised prompt context facets.
/// Production collectors emit these labels; tests prove an assembled
/// context+status pair is incomplete unless all six are present.
/// </summary>
public static class PromptContextFacetPolicy
{
    public const string ThoughtsMarker = "Memory:";
    public const string MoodMarker = "Mood:";
    public const string TraitsMarker = "Traits:";
    public const string RelationsMarker = "Social:";
    public const string JobMarker = "Job:";
    public const string SituationMarker = "Nearby:";

    public static bool HasRequiredFacets(string pawnContext, string status)
    {
        return Contains(pawnContext, ThoughtsMarker)
            && Contains(pawnContext, MoodMarker)
            && Contains(pawnContext, TraitsMarker)
            && Contains(pawnContext, RelationsMarker)
            && Contains(status, JobMarker)
            && Contains(status, SituationMarker);
    }

    public static string AssembleContext(string traits, string mood, string thoughts, string relations)
    {
        return (traits ?? "") + "\n" + (mood ?? "") + "\n" + (thoughts ?? "") + "\n" + (relations ?? "");
    }

    public static string AssembleStatus(string job, string situation)
    {
        return (job ?? "") + "\n" + (situation ?? "");
    }

    static bool Contains(string text, string marker) =>
        !string.IsNullOrEmpty(text) && text.IndexOf(marker, System.StringComparison.Ordinal) >= 0;
}
