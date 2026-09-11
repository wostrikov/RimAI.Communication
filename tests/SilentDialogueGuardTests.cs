using System;
using System.IO;
using Ustas.RimAI.Communication.Service;

/// <summary>
/// Ways the talk path used to go silent or throw: a colony silenced for good by a stale busy
/// flag or a remembered enemy, crashes on a null recipient or a failed persona query, and the
/// "dummy relation" error from hidden factions.
/// </summary>
internal static class SilentDialogueGuardTests
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

        var start = new DateTime(2026, 9, 10, 12, 0, 0);
        T(!BusyGate.IsStuck(false, start, start.AddHours(1)), "idle-is-never-stuck");
        T(!BusyGate.IsStuck(true, null, start.AddHours(1)), "unstamped-is-never-released");
        T(!BusyGate.IsStuck(true, start, start.AddSeconds(BusyGate.StuckAfterSeconds - 1)), "slow-request-is-left-alone");
        T(BusyGate.IsStuck(true, start, start.AddSeconds(BusyGate.StuckAfterSeconds)), "held-past-the-limit-is-stuck");
        T(BusyGate.IsStuck(true, start, start.AddSeconds(10), stuckAfterSeconds: 5), "limit-is-a-parameter");

        string ai = Read("AIService.cs.src");
        string state = Read("PawnState.cs.src");
        string pawn = Read("PawnUtil.cs.src");
        string prompt = Read("PromptManager.cs.src");
        string talk = Read("TalkService.cs.src");
        T(ai.Contains("volatile bool _busy"), "busy-flag-is-volatile");
        T(ai.Contains("BusyGate.IsStuck"), "busy-flag-has-a-backstop");
        T(state.Contains("volatile bool _isGeneratingTalk"), "generating-flag-is-volatile");
        T(pawn.Contains("IsLiveThreat(pawn, pawn.mindState?.enemyTarget)"), "remembered-enemy-must-be-live");
        T(!pawn.Contains("CapableOf(PawnCapacityDefOf.Moving)) return true"), "immobile-is-not-danger");
        T(pawn.Contains("pawn.Faction.def.hidden"), "hidden-factions-skipped");
        T(pawn.Contains("pawn != null && pawn == Cache.GetPlayer()"), "null-is-not-the-player");
        T(prompt.Contains("finally"), "base-instruction-restored-on-throw");
        int decorate = prompt.IndexOf("PromptService.DecoratePrompt(talkRequest", StringComparison.Ordinal);
        int snapshot = prompt.IndexOf("context.PawnContext = talkRequest.Context", StringComparison.Ordinal);
        T(decorate >= 0 && snapshot > decorate, "pawn-context-read-after-decoration");
        T(talk.Contains("talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer()"), "null-recipient-guard");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
