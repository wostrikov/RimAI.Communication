using System;
using System.Collections.Generic;
using System.IO;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;

/// <summary>
/// Pawns talk to each other about what they feel. Most talk used to be aimed at nobody - no
/// recipient, a monologue whenever nobody stood near, abstract keyword topics - and the model
/// filled the gap by addressing the player.
/// </summary>
internal static class ConversationFocusTests
{
    // The default as the owner's settings still store it: Windows line endings, the language
    // written out, and the slave line that has since been replaced.
    private const string StoredOldDefault =
        "Відігравай персонажа RimWorld відповідно до його профілю\r\n\r\nПравила:\r\nЗберігай оригінальні імена (не перекладай їх)\r\n"
        + "Веди короткий діалог (лише мовою українська, 1–2 речення)\r\n\r\nРолі:\r\n"
        + "Полонений: насторожений і нерішучий; згадуй ув'язнення; благай або торгуйся\r\n"
        + CaptiveDemeanorPolicy.SupersededSlaveLineUkrainian + "\r\n"
        + "Відвідувач: ввічливий, допитливий і шанобливий; стався до інших відвідувачів із тієї самої групи як до супутників\r\n"
        + "Ворог: ворожий та агресивний; використовуй стислі накази й погрози\r\n\r\n"
        + "Монолог = 1 репліка. Розмова = 4–8 коротких реплік";

    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        // The base instruction: an untouched old default is upgraded, an edited one is kept.
        T(BaseInstructionLineage.IsFormerDefault(StoredOldDefault), "stored-old-default-is-recognized");
        string settledOld = CaptiveDemeanorPolicy.ReplaceSupersededRoleLines(StoredOldDefault).Replace("\r\n", "\n");
        T(BaseInstructionLineage.IsFormerDefault(settledOld), "old-default-after-role-line-fix-is-recognized");
        T(BaseInstructionLineage.IsFormerDefault(settledOld.Replace("мовою українська", "мовою English")),
            "old-default-in-another-language-is-recognized");
        string upgraded = BaseInstructionLineage.Upgrade(StoredOldDefault, "English");
        T(upgraded == BaseInstructionLineage.Ukrainian("українська"), "upgrade-keeps-the-stored-language");
        T(upgraded.Contains("Персонажі говорять між собою й ніколи не звертаються до гравця чи читача"), "upgrade-brings-the-talk-to-each-other-rule");
        T(BaseInstructionLineage.Upgrade("custom", "українська") == BaseInstructionLineage.Ukrainian("українська"), "upgrade-falls-back-to-the-current-language");
        T(!BaseInstructionLineage.IsFormerDefault(BaseInstructionLineage.Ukrainian("українська")), "current-default-is-not-former");
        T(!BaseInstructionLineage.IsFormerDefault(StoredOldDefault.Replace("Правила:\r\n", "Правила:\r\nГовори віршами\r\n")),
            "a-players-extra-rule-is-kept");
        T(!BaseInstructionLineage.IsFormerDefault(StoredOldDefault.Replace("1–2 речення", "1–3 речення")), "a-players-edit-is-kept");
        T(!BaseInstructionLineage.IsFormerDefault(null) && !BaseInstructionLineage.IsFormerDefault("  "), "nothing-stored");
        T(BaseInstructionLineage.LanguageOf(StoredOldDefault) == "українська", "stored-language-read");

        string constant = Read("Constant.cs.src");
        string manager = Read("PromptManager.cs.src");
        string settings = Read("CommunicationSettings.cs.src");
        T(constant.Contains("if (BaseInstructionLineage.IsFormerDefault(value)) return true;"), "legacy-check-knows-the-former-default");
        T(manager.Contains("baseEntry.Content = Constant.UpgradeLegacyDefaultInstruction(baseEntry.Content);"), "preset-copy-upgraded");
        T(settings.Contains("SimpleModeInstruction = Constant.UpgradeLegacyDefaultInstruction(SimpleModeInstruction);"), "simple-mode-copy-upgraded");

        // A pawn alone mostly keeps quiet.
        T(!MonologuePolicy.Admit(false, 1f, 0.0), "monologues-off-means-never");
        T(MonologuePolicy.Admit(true, 0.15f, 0.10), "inside-the-chance-speaks");
        T(!MonologuePolicy.Admit(true, 0.15f, 0.20), "outside-the-chance-is-silent");
        T(!MonologuePolicy.Admit(true, 0f, 0.0), "zero-chance-is-silent");
        T(MonologuePolicy.Admit(true, 5f, 0.99), "chance-clamped-to-one");
        T(Math.Abs(MonologuePolicy.DefaultChance - 0.15f) < 1e-6, "default-chance");

        // Feelings, with their sign and weight.
        T(ThoughtTopicPolicy.Format("Ate fine meal", 5f) == "Ate fine meal(+5)", "positive-thought-signed");
        T(ThoughtTopicPolicy.Format("Rotten corpse", -15.4f) == "Rotten corpse(-15)", "negative-thought-signed");
        var thoughts = new List<(string name, float offset)> { ("a", 2f), ("b", -12f), ("c", 0.2f), ("d", 6f), ("e", -3f) };
        var strongest = ThoughtTopicPolicy.Strongest(thoughts, t => t.offset, 3);
        T(strongest.Count == 3 && strongest[0].name == "b" && strongest[1].name == "d" && strongest[2].name == "e", "strongest-by-size");
        T(ThoughtTopicPolicy.Strongest(thoughts, t => t.offset, 10).TrueForAll(t => t.name != "c"), "a-feeling-of-nothing-is-left-out");

        var names = new[] { "b", "d", "e" };
        var offsets = new[] { -12f, 6f, -3f };
        T(ThoughtTopicPolicy.Pick(names, offsets, null, 0.0) == 0, "lowest-roll-takes-the-strongest");
        T(ThoughtTopicPolicy.Pick(names, offsets, null, 0.99) == 2, "highest-roll-takes-the-last");
        T(ThoughtTopicPolicy.Pick(names, offsets, "b", 0.0) == 1, "the-last-topic-is-skipped");
        T(ThoughtTopicPolicy.Pick(new[] { "b" }, new[] { -12f }, "b", 0.5) == -1, "only-the-last-topic-means-none");
        T(ThoughtTopicPolicy.Pick(Array.Empty<string>(), Array.Empty<float>(), null, 0.5) == -1, "no-thoughts-no-topic");

        string topic = ThoughtTopicPolicy.Compose("Ann", "Rotten corpse", -15f, "Bob", "Ate fine meal(+5)");
        T(topic == "Topic: Ann brings up 'Rotten corpse'(-15) with Bob; Bob reacts with their own (Ate fine meal(+5))", "topic-names-both");
        T(ThoughtTopicPolicy.Compose("Ann", "Rotten corpse", -15f, "Bob", null).Contains("Bob reacts from their own feelings"), "partner-without-thoughts");
        T(!ThoughtTopicPolicy.Compose("Ann", "Rotten corpse", -15f, null, null).Contains("player"), "alone-topic-addresses-nobody");

        // Wiring: who speaks, to whom, and about what.
        string tick = Read("TickManagerPatch.cs.src");
        string partner = Read("ConversationPartner.cs.src");
        string selector = Read("PawnSelector.cs.src");
        string thought = Read("ThoughtPatch.cs.src");
        string bubble = Read("BubblePatch.cs.src");
        string spontaneous = Read("SpontaneousDialogueType.cs.src");
        string builder = Read("ContextBuilder.cs.src");
        string topics = Read("TopicService.cs.src");
        string status = Read("PawnUtilStatusOps.cs.src");
        string page = Read("CommunicationBasicSettingsPage.cs.src");
        T(tick.Contains("ConversationPartner.NearestFor(selectedPawn)") && tick.Contains("ConversationPartner.MayTalkAlone(selectedPawn)"),
            "turn-looks-for-a-partner-first");
        T(tick.Contains("new TalkRequest(null, selectedPawn, partner)"), "fallback-talk-has-a-recipient");
        T(tick.Contains("request.Recipient = partner;"), "event-talk-has-a-recipient");
        T(tick.Contains("ConversationPartner.Aim(queued, partner)"), "queued-talk-has-a-recipient");
        T(partner.Contains("MonologuePolicy.Admit(settings.AllowMonologue, settings.MonologueChance"), "monologue-chance-applied");
        T(selector.Contains("Cache.GetRandomWeightedPawn(talkReadyPawns, ConversationWeight)"), "selection-prefers-company");
        T(thought.Contains("AddTalkRequest(GetThoughtLabel(thought), null, TalkType.Thought)"), "thought-not-aimed-at-self");
        T(thought.Contains("Math.Abs(offset) >= 3f"), "situational-thoughts-need-weight");
        T(bubble.Contains("TalkRequests.Any(r => r.TalkType == TalkType.Chitchat)") && !bubble.Contains("TalkRequests.Count > 0"),
            "chitchat-dropped-only-for-another-chitchat");
        T(spontaneous.Contains("speaks directly to {partnerName} by name"), "partner-named");
        T(spontaneous.Contains("never to the reader or player"), "monologue-addresses-nobody");
        int thoughtTopic = spontaneous.IndexOf("ThoughtTopicService.TryDrawTopic(mainPawn, partner, pawns)", StringComparison.Ordinal);
        int keywordTopic = spontaneous.IndexOf("TopicService.DrawHint(talkRequest, mainPawn)", StringComparison.Ordinal);
        T(thoughtTopic >= 0 && keywordTopic > thoughtTopic, "feelings-before-keywords");
        T(builder.Contains("ThoughtTopicService.DescribeStrongestThoughts(pawn,"), "context-lists-strongest-thoughts");
        T(topics.Contains("KeywordTopicChance = 0.20"), "fewer-abstract-topics");
        T(!status.Contains("user colony") && !status.Contains("your home") && !status.Contains("You are away"), "status-in-third-person");
        T(page.Contains("\"RimTalk.Settings.MonologueChance\"") && page.Contains("MonologueChance = Policy.MonologuePolicy.DefaultChance"),
            "monologue-chance-on-the-page");
        T(settings.Contains("\"monologueChance\""), "monologue-chance-saved");
        foreach (var language in new[] { "lang-en.xml.src", "lang-uk.xml.src" })
        {
            string keyed = Read(language);
            T(keyed.Contains("<RimTalk.Settings.MonologueChance>") && keyed.Contains("<RimTalk.Settings.MonologueChanceTooltip>"),
                "monologue-chance-translated-" + language);
        }
        return n;
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
}
