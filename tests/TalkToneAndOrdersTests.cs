using System;
using System.IO;
using Ustas.RimAI.Communication.Error;
using Ustas.RimAI.Communication.Policy;

/// <summary>
/// Stream and failure robustness, dialogue tone, event prompts and the player's standing orders:
/// the host-free policies exercised directly, the rest pinned in the sources they live in.
/// </summary>
internal static class TalkToneAndOrdersTests
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

        // Event prompts: the letter's label, and the description cut short.
        string raid = EventPromptPolicy.Compose("Talk about incident", "Raid", "Pirates are attacking.\n\nThey came from the north.");
        T(raid.StartsWith("(Talk about incident: Raid)\n["), "event-carries-label");
        T(raid.Contains("Pirates are attacking. They came from the north."), "event-paragraphs-joined");
        string message = EventPromptPolicy.Compose("Talk about incident", "Research finished", "Research finished");
        T(message == "(Talk about incident)\n[Research finished]", "message-label-not-doubled");
        string repeated = EventPromptPolicy.Compose("Talk about incident", "Cold snap", "Cold snap\n\nIt is freezing.", " (Target: Bob)");
        T(repeated == "(Talk about incident: Cold snap)\n[It is freezing. (Target: Bob)]", "label-prefix-removed-from-text");
        string longText = string.Join(" ", new string('a', 50) + ".", new string('b', 60) + ".", new string('c', 80) + ".");
        string cut = EventPromptPolicy.Shorten(longText);
        T(cut.Length <= EventPromptPolicy.MaxDescriptionLength && cut.EndsWith("."), "description-cut-at-sentence");
        string words = EventPromptPolicy.Shorten(string.Join(" ", new string[40]).Replace(" ", "word "));
        T(words.Length <= EventPromptPolicy.MaxDescriptionLength + 1 && words.EndsWith("…"), "description-cut-at-word");
        T(EventPromptPolicy.Shorten(new string('字', 60) + "。" + new string('字', 200)) == new string('字', 60) + "。", "cjk-sentence-end");

        // Player orders: kept short, each once; absent means unchanged, empty means withdrawn.
        T(PlayerOrderPolicy.Normalize(null) == null, "orders-absent-keeps");
        T(PlayerOrderPolicy.Normalize("") == "", "orders-empty-clears");
        T(PlayerOrderPolicy.Normalize("none") == "", "orders-none-clears");
        T(PlayerOrderPolicy.Normalize("guard the gate; Guard the gate;\n- cook dinner") == "guard the gate; cook dinner", "orders-deduped");
        T(PlayerOrderPolicy.Normalize("a;b;c;d;e;f;g").Split(';').Length == PlayerOrderPolicy.MaxOrders, "orders-capped");
        T(PlayerOrderPolicy.Normalize(new string('x', 300)).Length == PlayerOrderPolicy.MaxOrderLength, "order-clipped");

        // A run of failures is one notice.
        var gate = new FailureNoticeGate(TimeSpan.FromSeconds(30));
        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        T(gate.Record(t0), "first-failure-opens-notice");
        T(!gate.Record(t0.AddSeconds(1)), "second-failure-folds-into-queued-notice");
        T(gate.TakeForNotice(t0.AddSeconds(1)) == 2, "notice-counts-both");
        T(!gate.Record(t0.AddSeconds(10)), "quiet-period-holds");
        T(gate.Record(t0.AddSeconds(40)), "after-quiet-period-new-notice");
        T(gate.TakeForNotice(t0.AddSeconds(40)) == 2, "held-failure-counted-in-next-notice");

        string stream = Read("OpenAIStreamHandler.cs.src");
        string state = Read("PawnState.cs.src");
        string talk = Read("TalkService.cs.src");
        string pawnUtil = Read("PawnUtil.cs.src");
        string persona = Read("Hediff_Persona.cs.src");
        string handler = Read("AIErrorHandler.cs.src");
        string archive = Read("ArchivePatch.cs.src");
        string tone = Read("DialogueTone.cs.src");
        string spontaneous = Read("SpontaneousDialogueType.cs.src");
        string relations = Read("RelationsService.cs.src");
        string prompt = Read("PromptService.cs.src");
        string builder = Read("ContextBuilder.cs.src");
        string manager = Read("PromptManager.cs.src");
        string status = Read("PawnUtilStatusOps.cs.src");

        T(stream.Contains("choice?.FinishReason"), "null-choice-guarded");
        T(state.Contains("CancelGenerationInvolvingPawn(keepTypes)") && state.Contains("AIService.CurrentTalk"), "ignored-lines-stop-generation");
        T(state.Contains("MarkIgnored(response);") && state.Contains("log.SpokenTick = -1"), "every-ignore-marks-log");
        T(talk.Contains("if (!talk.TalkType.IsFromUser())") && talk.Contains("talk.ParentTalkId = Guid.Empty;"), "player-replies-survive-dropped-parent");
        T(!pawnUtil.Contains("if (pawn.Downed) return true;"), "downed-is-not-danger");
        T(persona.Contains("Constant.PersonaBaby") && persona.Contains("!pawn.IsBaby()"), "baby-persona-grows-up");
        T(handler.Contains("GenerationFailures.Record") && handler.Contains("MainThreadSchedulerAccess.RunInlineOrEnqueue"), "failures-collapsed-on-main-thread");
        T(archive.Contains("EventPromptPolicy.Compose("), "event-prompt-policy-used");
        T(tone.Contains("[Combat sides]") && tone.Contains("they have never met"), "tone-rules-present");
        T(spontaneous.Contains("IsBrawlingWithAlly") && spontaneous.Contains("DialogueTone.MentalBreak"), "brawl-and-break-tone");
        T(relations.Contains("pawn.Faction.IsPlayer && !pawn.IsPrisoner") && relations.Contains("GrudgeOpinionThreshold = -40f"), "colony-members-and-grudges");
        T(talk.Contains("PlayerOrderService.Capture(orderRecipient") && talk.Contains("PlayerOrderService.DrainCaptures()"), "orders-captured-and-applied");
        T(prompt.Contains("PlayerOrderService.ContextLine(pawn)") && builder.Contains("PlayerOrderService.Instruction("), "orders-fed-back");
        T(manager.Contains("Id = BuiltInPromptEntries.BaseInstructionId") && !manager.Contains("\"Base Instruction\""), "built-in-entries-have-ids");
        T(status.Contains("is attacking their own"), "berserk-ally-named");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }
}
