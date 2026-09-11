using System;
using System.IO;

/// <summary>
/// What the model is told about a pawn and its surroundings: words rather than bare numbers,
/// the surroundings as background context rather than part of the request, and a topic to
/// start from so the same pawns do not keep circling the same remarks.
/// </summary>
internal static class ContextWordingTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        string describer = Read("Describer.cs.src");
        string builder = Read("ContextBuilder.cs.src");
        string prompt = Read("PromptService.cs.src");
        string relations = Read("RelationsService.cs.src");

        T(describer.Contains("public static string Opinion(float value)"), "opinion-as-a-word");
        T(describer.Contains("public static string Condition(float pctHitPoints)"), "wear-as-a-word");
        T(describer.Contains("public static string Beauty(Pawn pawn)"), "beauty-from-the-pawns-need");
        T(!describer.Contains(".Translate()"), "model-facing-words-stay-english");

        T(!relations.Contains("ToStringWithSign"), "no-signed-opinion-numbers");
        T(relations.Contains("Describer.Opinion(opinionValue)"), "relations-use-opinion-words");
        T(!relations.Contains("\"Friend\".Translate()"), "relation-labels-stay-english");

        T(builder.Contains("GroupBy(s => s.LevelDescriptor)"), "skills-grouped-by-tier");
        T(builder.Contains("DescribeThingLabel"), "equipment-condition-as-a-word");
        T(builder.Contains("TopicService.DrawHint(talkRequest, mainPawn)"), "topic-keywords-offered");
        T(builder.Contains("if (!talkRequest.TopicHintDrawn)"), "topic-drawn-once-per-talk");
        int combat = builder.IndexOf("mainPawn.IsInCombat() || mainPawn.GetMapRole() == MapRole.Invading", StringComparison.Ordinal);
        int monologue = builder.IndexOf("short monologue", StringComparison.Ordinal);
        T(combat >= 0 && monologue > combat, "combat-outranks-monologue");

        int decorate = prompt.IndexOf("public static void DecoratePrompt", StringComparison.Ordinal);
        int build = prompt.IndexOf("public static string BuildContext", StringComparison.Ordinal);
        T(prompt.Contains("context.AppendLine(\"[Environment]\")"), "surroundings-in-the-context");
        T(decorate > 0 && !prompt.Substring(decorate).Contains("contextSettings.IncludeWeather"), "surroundings-not-in-the-prompt");
        T(build >= 0, "context-builder-present");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
