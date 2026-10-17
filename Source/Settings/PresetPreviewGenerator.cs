using System;
using Ustas.RimAI.Communication.Prompt;
using Verse;

namespace Ustas.RimAI.Communication;

public static class PresetPreviewGenerator
{
    public static string GeneratePreview(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) return "";
        
        if (Current.ProgramState != ProgramState.Playing)
        {
            return "RimTalk.Settings.PromptPreset.PreviewNotAvailable".Translate();
        }

        // 1. Leverage the last properly generated PromptContext
        PromptContext ctx = PromptManager.LastContext;

        // 2. If no request has been sent yet, inform the user
        if (ctx == null)
        {
            return "RimTalk.Settings.PromptPreset.NoRecentInteraction".Translate();
        }

        try
        {
            // Mark as preview to handle UI-specific logic (like chat history placeholders)
            ctx.IsPreview = true;
            // The preview shows why a template fails, which is what the talk itself now drops.
            return ScribanParser.TryRender(template, ctx, out string rendered, out string error)
                ? rendered
                : "RimTalk.Settings.PromptPreset.PreviewError".Translate(error).ToString();
        }
        catch (Exception ex)
        {
            return "RimTalk.Settings.PromptPreset.PreviewError".Translate(ex.Message);
        }
    }
}
