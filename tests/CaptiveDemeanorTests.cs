using System;
using System.IO;
using Ustas.RimAI.Communication.Data;

/// <summary>
/// A slave's lines follow the slave's state. The instruction used to tell every slave to
/// "reference forced labor and exhaustion", and one who had slept twelve hours with nothing
/// to do and suppression at a hundred percent said so in every sentence.
/// </summary>
internal static class CaptiveDemeanorTests
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

        string broken = CaptiveDemeanorPolicy.SlaveDemeanor(100f, exhausted: false);
        T(broken.StartsWith("Demeanor: will broken", StringComparison.Ordinal), "full-suppression-is-broken");
        T(broken.Contains("never complains about work or servitude"), "broken-does-not-complain");
        T(!broken.Contains("short of sleep"), "rested-says-nothing-of-sleep");
        T(CaptiveDemeanorPolicy.SlaveDemeanor(80f, false) == broken, "eighty-is-already-broken");
        T(CaptiveDemeanorPolicy.SlaveDemeanor(79.9f, false).Contains("obedient and guarded"), "below-eighty-is-obedient");
        T(CaptiveDemeanorPolicy.SlaveDemeanor(35f, false).Contains("sullen"), "low-suppression-grumbles");
        T(CaptiveDemeanorPolicy.SlaveDemeanor(5f, false).Contains("defiant"), "lowest-is-defiant");
        T(CaptiveDemeanorPolicy.SlaveDemeanor(100f, exhausted: true).EndsWith("short of sleep right now", StringComparison.Ordinal),
            "tiredness-only-when-tired");
        foreach (float level in new[] { 0f, 30f, 60f, 100f })
        {
            string line = CaptiveDemeanorPolicy.SlaveDemeanor(level, false);
            T(!line.Contains("forced labor") && !line.Contains("exhaust"), "no-standing-order-to-lament-" + level);
        }

        string stored = "Правила:\nмоє власне правило\n\nРолі:\n" + CaptiveDemeanorPolicy.SupersededSlaveLineUkrainian + "\nВорог: ворожий";
        string settled = CaptiveDemeanorPolicy.ReplaceSupersededRoleLines(stored);
        T(!settled.Contains("згадуй примусову працю"), "stored-line-replaced");
        T(settled.Contains(CaptiveDemeanorPolicy.SlaveLineUkrainian), "new-line-in-its-place");
        T(settled.Contains("моє власне правило") && settled.Contains("Ворог: ворожий"), "the-players-edits-survive");
        T(CaptiveDemeanorPolicy.ReplaceSupersededRoleLines(settled) == settled, "replacing-twice-changes-nothing");
        T(CaptiveDemeanorPolicy.ReplaceSupersededRoleLines("Roles:\n" + CaptiveDemeanorPolicy.SupersededSlaveLineEnglish)
            == "Roles:\n" + CaptiveDemeanorPolicy.SlaveLineEnglish, "english-line-replaced");
        T(CaptiveDemeanorPolicy.ReplaceSupersededRoleLines(null) == null, "nothing-stored");
        T(CaptiveDemeanorPolicy.ReplaceSupersededRoleLines("Раб: мовчить") == "Раб: мовчить", "a-rewritten-line-is-left-alone");

        string constant = Read("Constant.cs.src");
        string prompt = Read("PromptService.cs.src");
        string builder = Read("ContextBuilder.cs.src");
        T(constant.Contains("public static string DefaultInstruction => BaseInstructionLineage.Ukrainian(Lang);"),
            "default-instruction-is-the-lineage-current");
        string current = BaseInstructionLineage.Ukrainian("українська");
        T(current.Contains(CaptiveDemeanorPolicy.SlaveLineUkrainian), "default-instruction-takes-the-new-line");
        T(!current.Contains("згадуй примусову працю"), "default-instruction-lost-the-old-line");
        T(prompt.Contains("ContextBuilder.GetCaptiveDemeanorContext(pawn)"), "demeanor-reaches-the-pawn-context");
        int demeanor = builder.IndexOf("public static string GetCaptiveDemeanorContext", StringComparison.Ordinal);
        int next = builder.IndexOf("public static string GetRelationsContext", StringComparison.Ordinal);
        T(demeanor >= 0 && next > demeanor
            && builder.IndexOf("IncludePrisonerSlaveStatus", demeanor, next - demeanor, StringComparison.Ordinal) < 0,
            "demeanor-does-not-wait-for-the-status-option");
        return n;
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name));
}
