using System;
using System.IO;
using Ustas.RimAI.Communication.Data;

/// <summary>
/// Announcements, fast-track interactions, the player persona and the provider defaults:
/// who may cut in on whom, and that each piece is wired where the talk path reads it.
/// </summary>
internal static class FeaturePortTests
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

        T(TalkType.Announcement.IsFromUser(), "announcement-is-the-players");
        T(TalkType.Interaction.IsFastTrack() && TalkType.Urgent.IsFastTrack(), "interaction-and-urgent-are-fast");
        T(!TalkType.Chitchat.IsFastTrack() && !TalkType.Sleep.IsFastTrack(), "chitchat-and-sleep-wait-their-turn");
        T(TalkType.User.CanPreempt(TalkType.Urgent), "player-cuts-into-anything");
        T(TalkType.Interaction.CanPreempt(TalkType.Chitchat), "interaction-cuts-into-background");
        T(!TalkType.Interaction.CanPreempt(TalkType.Urgent), "interaction-does-not-cut-into-urgent");
        T(!TalkType.Chitchat.CanPreempt(TalkType.Other), "background-never-cuts-in");

        string ai = Read("AIService.cs.src");
        string talk = Read("TalkService.cs.src");
        string openAi = Read("OpenAIClient.cs.src");
        string persona = Read("PersonaService.cs.src");
        string prompt = Read("PromptService.cs.src");
        string builder = Read("ContextBuilder.cs.src");

        T(ai.Contains("cts.Token,"), "cancellation-reaches-the-provider-chain");
        T(ai.Contains("public static void CancelCurrent()"), "a-request-can-be-cancelled");
        T(openAi.Contains("AIService.IsCancellationRequested()"), "http-request-aborts-on-cancel");
        T(openAi.Contains("if (m.Contains(\"gemini\")) return \"low\";"), "every-gemini-gets-low-effort");
        T(talk.Contains("PawnSelector.GetAllNearByPawns(talkRequest.Initiator, isAnnouncement: talkRequest.IsAnnouncement)"), "announcement-hears-further");
        T(talk.Contains("catch (OperationCanceledException)"), "cancelled-talk-is-quiet");
        T(builder.Contains("announced to everyone nearby"), "announcement-intent");
        T(builder.Contains("AIDrivenPawnOnly && pawns.Count > 2"), "pawn-only-mode-keeps-players-lines");
        T(persona.Contains("Settings.Get().PlayerPersona"), "player-persona-read-and-written");
        T(prompt.Contains("(Player)\\nPersonality:"), "player-persona-in-context");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
