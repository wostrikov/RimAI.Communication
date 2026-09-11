using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ustas.RimAI.Communication.API;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using Ustas.RimAI.Core.Personas;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// All public methods in this class are designed to be patchable with Harmony.
/// Use Prefix to replace functionality, Postfix to extend it.
/// </summary>
public static class PromptService
{
    public enum InfoLevel { Short, Normal, Full }

    public static string BuildContext(List<Pawn> pawns, bool isAnnouncement = false)
    {
        TalkLifecycle.PublishContextBuildStarted(pawns);
        try
        {
        var context = new StringBuilder();
    
        for (int i = 0; i < pawns.Count; i++)
        {
            var pawn = pawns[i];
            if (pawn.IsPlayer())
            {
                // When the model speaks for the player it needs to know who that is.
                var playerPersona = Settings.Get().PlayerPersona;
                if (Settings.Get().PlayerDialogueMode == Settings.PlayerDialogueMode.AIDriven &&
                    !string.IsNullOrWhiteSpace(playerPersona))
                {
                    string playerContext = $"{pawn.LabelShort} (Player)\nPersonality: {playerPersona.Trim()}";
                    var playerState = Cache.Get(pawn);
                    if (playerState != null) playerState.Context = playerContext;
                    context.AppendLine($"[P{i + 1}]").AppendLine(playerContext);
                }
                continue;
            }

            // Listeners to an announcement only react: a one-line profile each keeps a crowd of
            // eight inside the budget a full context for each would blow.
            if (isAnnouncement && i > 0)
            {
                var listenerContext = CreateMinimalListenerContext(pawn);
                var listenerState = Cache.Get(pawn);
                if (listenerState != null) listenerState.Context = listenerContext;
                context.AppendLine($"[P{i + 1}]").AppendLine(listenerContext);
                continue;
            }

            InfoLevel infoLevel = Settings.Get().Context.EnableContextOptimization 
                                  || i != 0 ? InfoLevel.Short : InfoLevel.Normal;
            var pawnContext = CreatePawnContext(pawn, infoLevel);
            pawnContext = CommonUtil.StripFormattingTags(pawnContext);

            Cache.Get(pawn).Context = pawnContext;
            context.AppendLine($"[P{i + 1}]").AppendLine(pawnContext);
        }

        // The surroundings are background, not the request: carried in the context they
        // inform the line, while in the prompt the model kept talking about the weather.
        if (pawns.Count > 0 && pawns[0] != null)
        {
            var environment = BuildEnvironmentContextString(pawns[0]);
            if (!string.IsNullOrWhiteSpace(environment))
                context.AppendLine("[Environment]").AppendLine(environment);
        }

            return context.ToString().TrimEnd();
        }
        finally
        {
            TalkLifecycle.PublishContextBuildCompleted();
        }
    }

    /// <summary>One line for a listener: role, traits and mood.</summary>
    public static string CreateMinimalListenerContext(Pawn pawn)
    {
        var role = pawn.GetRole(false) ?? "Colonist";
        var traits = pawn.story?.traits?.TraitsSorted?
            .Select(t => t.LabelCap.ToString())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();
        var traitsStr = traits is { Count: > 0 } ? $", Traits: {string.Join(", ", traits)}" : "";
        var mood = pawn.needs?.mood?.MoodString;
        var moodStr = !string.IsNullOrEmpty(mood) ? $" | Mood: {mood}" : "";
        return $"{pawn.LabelShort} ({role}{traitsStr}){moodStr}";
    }

    /// <summary>Time, date, season, weather, location, surroundings and wealth of the main pawn's map.</summary>
    public static string BuildEnvironmentContextString(Pawn mainPawn)
    {
        if (mainPawn?.Map == null) return string.Empty;

        var contextSettings = Settings.Get().Context;
        var sb = new StringBuilder();
        var gameData = CommonUtil.GetInGameData();

        if (contextSettings.IncludeTime)
            sb.Append($"Time: {ApplyEnvironmentWithHook(mainPawn.Map, ContextCategories.Environment.Time, gameData.Hour12HString)}");
        if (contextSettings.IncludeDate)
            sb.Append($"\nToday: {ApplyEnvironmentWithHook(mainPawn.Map, ContextCategories.Environment.Date, gameData.DateString)}");
        if (contextSettings.IncludeSeason)
            sb.Append($"\nSeason: {ApplyEnvironmentWithHook(mainPawn.Map, ContextCategories.Environment.Season, gameData.SeasonString)}");
        if (contextSettings.IncludeWeather)
            sb.Append($"\nWeather: {ApplyEnvironmentWithHook(mainPawn.Map, ContextCategories.Environment.Weather, gameData.WeatherString)}");

        ContextBuilder.BuildLocationContext(sb, contextSettings, mainPawn);
        ContextBuilder.BuildEnvironmentContext(sb, contextSettings, mainPawn);

        if (contextSettings.IncludeWealth)
            sb.Append($"\nWealth: {ApplyEnvironmentWithHook(mainPawn.Map, ContextCategories.Environment.Wealth, Describer.Wealth(mainPawn.Map.wealthWatcher.WealthTotal))}");

        return sb.ToString().Trim();
    }

    /// <summary>Creates the basic pawn backstory section.</summary>
    public static string CreatePawnBackstory(Pawn pawn, InfoLevel infoLevel = InfoLevel.Normal)
    {
        var sb = new StringBuilder();
        var name = pawn.LabelShort;
        var pawnTitle = pawn.GetTitle();
        var title = string.IsNullOrWhiteSpace(pawnTitle) ? "" : $" ({pawnTitle})";
        var genderAndAge = Regex.Replace(pawn.MainDesc(false), @"\(\d+\)", "").Trim();
        sb.AppendLine($"{name}{title} ({genderAndAge})");

        var role = pawn.GetRole(true);
        if (role != null)
            sb.AppendLine($"Role: {role}");

        // Each section applies hooks via AppendWithHook
        AppendWithHook(sb, pawn, ContextCategories.Pawn.Race, ContextBuilder.GetRaceContext(pawn, infoLevel));
        
        if (infoLevel != InfoLevel.Short && !pawn.IsVisitor() && !pawn.IsEnemy())
            AppendWithHook(sb, pawn, ContextCategories.Pawn.Genes, ContextBuilder.GetNotableGenesContext(pawn, infoLevel));
        
        AppendWithHook(sb, pawn, ContextCategories.Pawn.Ideology, ContextBuilder.GetIdeologyContext(pawn, infoLevel));

        // Stop here for invaders and visitors
        if ((pawn.IsEnemy() || pawn.IsVisitor()) && !pawn.IsQuestLodger())
            return sb.ToString();

        AppendWithHook(sb, pawn, ContextCategories.Pawn.Backstory, ContextBuilder.GetBackstoryContext(pawn, infoLevel));
        AppendWithHook(sb, pawn, ContextCategories.Pawn.Traits, ContextBuilder.GetTraitsContext(pawn, infoLevel));
        
        if (infoLevel != InfoLevel.Short)
            AppendWithHook(sb, pawn, ContextCategories.Pawn.Skills, ContextBuilder.GetSkillsContext(pawn, infoLevel));

        return sb.ToString();
    }

    /// <summary>Creates the full pawn context.</summary>
    public static string CreatePawnContext(Pawn pawn, InfoLevel infoLevel = InfoLevel.Normal)
    {
        var sb = new StringBuilder();
        sb.Append(CreatePawnBackstory(pawn, infoLevel));

        // Each section applies hooks via AppendWithHook
        AppendWithHook(sb, pawn, ContextCategories.Pawn.Health, ContextBuilder.GetHealthContext(pawn, infoLevel));

        var personality = Cache.Get(pawn)?.Personality;
        string presented = personality ?? string.Empty;
        if (PromptManager.LastContext != null
            && !string.IsNullOrEmpty(pawn.ThingID)
            && PromptManager.LastContext.TryGetTypedPersonaProjection(pawn.ThingID, out var projection)
            && projection != null)
        {
            presented = projection;
        }
        else if (PersonaProjectionAccess.Current != null && !string.IsNullOrEmpty(pawn.ThingID))
        {
            var result = PersonaProjectionAccess.Current.GetProjection(
                pawn.ThingID, personality ?? string.Empty, pawn);
            presented = result?.Projection ?? personality ?? string.Empty;
        }

        if (PersonaGeneratePersistPolicy.ShouldInjectTalk(presented))
            sb.Append(PersonaGeneratePersistPolicy.FormatTalkLine(presented));

        // Stop here for invaders
        if (pawn.IsEnemy())
            return TalkLifecycle.TransformPawnContext(pawn, sb.ToString());

        AppendWithHook(sb, pawn, ContextCategories.Pawn.Mood, ContextBuilder.GetMoodContext(pawn, infoLevel));
        AppendWithHook(sb, pawn, ContextCategories.Pawn.Thoughts, ContextBuilder.GetThoughtsContext(pawn, infoLevel));
        AppendWithHook(sb, pawn, ContextCategories.Pawn.CaptiveStatus, ContextBuilder.GetPrisonerSlaveContext(pawn, infoLevel));
        
        // Visitor activity
        if (pawn.IsVisitor())
        {
            var lord = pawn.GetLord() ?? pawn.CurJob?.lord;
            if (lord?.LordJob != null)
            {
                var cleanName = lord.LordJob.GetType().Name.Replace("LordJob_", "");
                sb.AppendLine($"Activity: {cleanName}");
            }
        }

        AppendWithHook(sb, pawn, ContextCategories.Pawn.Social, ContextBuilder.GetRelationsContext(pawn, infoLevel));
        
        if (infoLevel != InfoLevel.Short)
            AppendWithHook(sb, pawn, ContextCategories.Pawn.Equipment, ContextBuilder.GetEquipmentContext(pawn, infoLevel));

        return TalkLifecycle.TransformPawnContext(pawn, sb.ToString());
    }

    /// <summary>Decorates the prompt with dialogue type and status; the surroundings travel in the context.</summary>
    public static void DecoratePrompt(TalkRequest talkRequest, List<Pawn> pawns, string status)
    {
        TalkLifecycle.PublishPromptDecorateStarted(pawns);
        var sb = new StringBuilder();
        var mainPawn = pawns[0];
        var shortName = $"{mainPawn.LabelShort}";

        // Dialogue type
        ContextBuilder.BuildDialogueType(sb, talkRequest, pawns, shortName, mainPawn);
        sb.Append($"\n{status}");

        if (AIService.IsFirstInstruction() || talkRequest.TalkType.IsFromUser())
        {
            if (DialogueLanguage.TryGetDialogueInstruction(out var instruction))
                sb.Append($"\n{instruction}");
            else
                sb.Append($"\nRespond only in {Constant.Lang}.");
        }

        talkRequest.Prompt = sb.ToString();
        TalkLifecycle.PublishPromptDecorated(talkRequest, pawns, status);
    }
    
    /// <summary>
    /// Appends text to StringBuilder if not empty, with optional hook application.
    /// </summary>
    private static void AppendIfNotEmpty(StringBuilder sb, string text)
    {
        if (!string.IsNullOrEmpty(text))
            sb.AppendLine(text);
    }
    
    /// <summary>
    /// Appends pawn context text with hook and injection application.
    /// </summary>
    private static void AppendWithHook(StringBuilder sb, Pawn pawn, ContextCategory category, string text)
    {
        // Render Before injections
        if (ContextHookRegistry.HasAnyInjections)
            foreach (var (_, pos, _, provider) in ContextHookRegistry.GetInjectedSectionsAt(category))
                if (pos == ContextHookRegistry.InjectPosition.Before && provider is Func<Pawn, string> p)
                    AppendIfNotEmpty(sb, p(pawn));
        
        // Apply hooks (always call to allow Override hooks on empty categories)
        var hooked = ContextHookRegistry.ApplyPawnHooks(category, pawn, text ?? "");
        AppendIfNotEmpty(sb, hooked);
        
        // Render After injections
        if (ContextHookRegistry.HasAnyInjections)
            foreach (var (_, pos, _, provider) in ContextHookRegistry.GetInjectedSectionsAt(category))
                if (pos == ContextHookRegistry.InjectPosition.After && provider is Func<Pawn, string> p)
                    AppendIfNotEmpty(sb, p(pawn));
    }
    
    /// <summary>
    /// Appends environment context text with hook and injection application.
    /// </summary>
    private static string ApplyEnvironmentWithHook(Map map, ContextCategory category, string text)
    {
        var sb = new StringBuilder();
        
        // Render Before injections
        if (ContextHookRegistry.HasAnyInjections)
            foreach (var (_, pos, _, provider) in ContextHookRegistry.GetInjectedSectionsAt(category))
                if (pos == ContextHookRegistry.InjectPosition.Before && provider is Func<Map, string> p)
                    AppendIfNotEmpty(sb, p(map));
        
        // Apply hooks
        var hooked = ContextHookRegistry.ApplyEnvironmentHooks(category, map, text ?? "");
        AppendIfNotEmpty(sb, hooked);
        
        // Render After injections
        if (ContextHookRegistry.HasAnyInjections)
            foreach (var (_, pos, _, provider) in ContextHookRegistry.GetInjectedSectionsAt(category))
                if (pos == ContextHookRegistry.InjectPosition.After && provider is Func<Map, string> p)
                    AppendIfNotEmpty(sb, p(map));
        
        return sb.ToString().TrimEnd();
    }
}
