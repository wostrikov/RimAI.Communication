using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;

/// <summary>
/// The event filter's separate message keys, the capped API log, the record of who has met whom,
/// and the source-level fixes around them: template failures, combat, Player2 notices and the
/// talk float menu.
/// </summary>
internal static class ReviewFixesTests
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

        // Event filter keys: message defs are prefixed, letters keep their bare names.
        T(ArchivableFilterKeys.ForMessageDef("ThreatBig") == "Verse.Message:ThreatBig", "message-key-prefixed");
        T(ArchivableFilterKeys.IsMessageKey("Verse.Message") && ArchivableFilterKeys.IsMessageKey("Verse.Message:NegativeEvent"), "message-keys-recognized");
        T(!ArchivableFilterKeys.IsMessageKey("ThreatBig") && !ArchivableFilterKeys.IsMessageKey("RimWorld.ChoiceLetter"), "letter-keys-not-message-keys");
        T(ArchivableFilterKeys.DisplayName("Verse.Message:ThreatBig") == "ThreatBig" && ArchivableFilterKeys.DisplayName("ThreatBig") == "ThreatBig", "display-name-drops-prefix");

        // The live config that hid raid letters: five shared names and a message-only one, all off.
        var stored = new Dictionary<string, bool>
        {
            ["Verse.Message"] = false,
            ["ThreatBig"] = false,
            ["ThreatSmall"] = false,
            ["NegativeEvent"] = false,
            ["PositiveEvent"] = false,
            ["NeutralEvent"] = false,
            ["SilentInput"] = true,
            ["RimWorld.ChoiceLetter"] = true,
        };
        string[] messageDefs = ["ThreatBig", "ThreatSmall", "NegativeEvent", "PositiveEvent", "NeutralEvent", "SilentInput", "TaskCompletion"];
        T(ArchivableFilterKeys.MigrateBareMessageKeys(stored, messageDefs), "migration-reports-change");
        T(!stored.ContainsKey("ThreatBig") && !stored.ContainsKey("NegativeEvent"), "letter-names-back-to-default");
        T(stored["Verse.Message:ThreatBig"] == false && stored["Verse.Message:NeutralEvent"] == false, "message-values-kept");
        T(stored["Verse.Message:SilentInput"] && !stored.ContainsKey("SilentInput"), "message-only-def-moved");
        T(!stored.ContainsKey("Verse.Message:TaskCompletion"), "absent-def-not-invented");
        T(stored["Verse.Message"] == false && stored["RimWorld.ChoiceLetter"], "parent-and-type-keys-untouched");
        T(!ArchivableFilterKeys.MigrateBareMessageKeys(stored, messageDefs), "second-run-is-a-no-op");

        var both = new Dictionary<string, bool> { ["ThreatBig"] = false, ["Verse.Message:ThreatBig"] = true };
        ArchivableFilterKeys.MigrateBareMessageKeys(both, ["ThreatBig"]);
        T(both["Verse.Message:ThreatBig"] && !both.ContainsKey("ThreatBig"), "existing-prefixed-value-wins");
        T(!ArchivableFilterKeys.MigrateBareMessageKeys(null, messageDefs), "null-dictionary-safe");

        // API log: capped, oldest first, and readers get a copy.
        var history = new BoundedHistory<int, string>(3);
        for (int i = 1; i <= 5; i++) history.Set(i, "v" + i);
        T(history.Count == 3, "history-capped");
        T(!history.TryGetValue(1, out _) && !history.TryGetValue(2, out _), "oldest-dropped");
        T(string.Join(",", history.Snapshot()) == "v3,v4,v5", "insertion-order-kept");
        history.Set(4, "v4b");
        T(string.Join(",", history.Snapshot()) == "v3,v4b,v5", "replace-keeps-position");
        var snapshot = history.Snapshot();
        history.Set(6, "v6");
        T(snapshot.Count == 3 && snapshot[0] == "v3", "snapshot-is-a-copy");
        foreach (var _ in history.Snapshot()) history.Set(100 + n, "during-enumeration");
        T(history.Count == 3, "write-while-enumerating-snapshot");
        history.Clear();
        T(history.Count == 0 && history.Snapshot().Count == 0, "history-cleared");
        T(ApiHistorySource().Contains("BoundedHistory<Guid, ApiLog> History = new(MaxEntries)") && ApiHistorySource().Contains("MaxEntries = 500"), "api-history-bounded");

        // Who has met whom: symmetric, capped, restored from a save.
        var met = new MetPairRegistry(capacity: 3);
        T(MetPairRegistry.Key(5, 9) == MetPairRegistry.Key(9, 5), "pair-key-symmetric");
        T(MetPairRegistry.Key(1, 2) != MetPairRegistry.Key(1, 3), "pair-keys-distinct");
        T(met.Record(5, 9) && met.HasMet(9, 5), "recorded-pair-has-met");
        T(!met.Record(9, 5), "pair-recorded-once");
        T(!met.Record(4, 4) && !met.HasMet(4, 4), "self-pair-ignored");
        met.Record(1, 2); met.Record(1, 3); met.Record(1, 4);
        T(met.Count == 3 && !met.HasMet(5, 9) && met.HasMet(1, 4), "oldest-pair-dropped");
        var restored = new MetPairRegistry(capacity: 3);
        restored.Load(met.Snapshot());
        T(restored.HasMet(1, 2) && restored.HasMet(1, 4) && restored.Count == 3, "pairs-survive-save");
        restored.Load(null);
        T(restored.Count == 0, "old-save-loads-empty");

        string parser = Read("ScribanParser.cs.src");
        string manager = Read("PromptManager.cs.src");
        string talk = Read("TalkService.cs.src");
        string player2 = Read("Player2Client.cs.src");
        string floatMenu = Read("FloatMenuPatch.cs.src");
        string combat = Read("PawnCombat.cs.src");
        string tone = Read("DialogueTone.cs.src");
        string world = Read("RimTalkWorldComponent.cs.src");
        string filterPage = Read("CommunicationEventFilterSettingsPage.cs.src");

        T(parser.Contains("public static bool TryRender(") && !parser.Contains("return templateText;\n            }"), "parse-error-not-returned-raw");
        T(manager.Contains("ScribanParser.RenderEntry(") && !manager.Contains("ScribanParser.Render(entry.Content"), "talk-entries-render-strictly");
        T(manager.Contains("talkRequest.PromptRenderFailure = context.RenderFailure;"), "failure-reaches-request");
        T(talk.Contains("talkRequest.PromptRenderFailure != null") && talk.Contains("AIErrorHandler.ReportPromptRenderFailure("), "failed-template-aborts-talk");
        T(!player2.Contains("LongEventHandler.ExecuteWhenFinished(() =>") && player2.Contains("MainThreadSchedulerAccess.RunInlineOrEnqueue"), "player2-notices-on-main-thread");
        T(!floatMenu.Contains("selectedPawn.Drafted) return;") && floatMenu.Contains("Danger.Deadly : Danger.Some"), "drafted-pawns-offered-talk");
        T(floatMenu.Contains("if (dx != 0 || dz != 0)"), "clicked-cell-skips-distance-test");
        T(combat.Contains("Verb_CastAbility") && combat.Contains("JobDefOf.Hunt") && combat.Contains("if (!IsHostilePair(target, pawn)) return false;"), "combat-needs-hostility");
        T(tone.Contains("EncounterMemory.HaveMet(") && tone.Contains("IsSameGroup(") && tone.Contains("mainPawn.IsPrisoner"), "first-meeting-narrowed");
        T(talk.Contains("EncounterMemory.RecordTalk(talkRequest, receivedResponses)"), "talk-records-meeting");
        T(world.Contains("\"metPawnPairs\""), "met-pairs-saved");
        T(!filterPage.Contains("children.RemoveAll(child => messageKeys.Contains(child))") && filterPage.Contains("ArchivableFilterKeys.ForMessageDef(def.defName)"), "filter-page-keys-messages-apart");
        return n;
    }

    static string ApiHistorySource() => Read("ApiHistory.cs.src");

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }
}
