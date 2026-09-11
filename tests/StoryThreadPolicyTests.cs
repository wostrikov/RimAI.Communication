using System;
using System.IO;
using Ustas.RimAI.Communication.Policy;

/// <summary>A story one pawn tells, others carry on, and someone finally ends.</summary>
internal static class StoryThreadPolicyTests
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

        T(StoryThreadPolicy.OpensThread("storytelling", "childhood"), "storytelling-opens");
        T(StoryThreadPolicy.OpensThread("Rumor", "hope"), "approach-case-insensitive");
        T(StoryThreadPolicy.OpensThread("banter", "frontier legends"), "legend-subject-opens");
        T(!StoryThreadPolicy.OpensThread("banter", "taste in music"), "banter-about-music-does-not");

        string middle = StoryThreadPolicy.ContinuationInstruction(1, "Леся", "strange rumors", "a light in the ruins");
        string last = StoryThreadPolicy.ContinuationInstruction(StoryThreadPolicy.MaxSteps - 1, "Леся", "strange rumors", "a light in the ruins");
        T(middle.Contains("Леся") && middle.Contains("a light in the ruins"), "continuation-names-teller-and-story");
        T(!middle.Contains("ending") && last.Contains("ending"), "last-step-asks-for-an-ending");

        T(StoryThreadPolicy.Summarize(["Леся: одна", "  ", "Борсук: друга"]) == "Леся: одна / Борсук: друга", "summary-joins-lines");
        string longText = new string('a', 1000);
        T(StoryThreadPolicy.Summarize([longText]).Length == StoryThreadPolicy.MaxSummaryChars, "summary-is-cut");
        string extended = StoryThreadPolicy.Extend(new string('x', 590), "the end");
        T(extended.Length == StoryThreadPolicy.MaxSummaryChars && extended.EndsWith("the end"), "extend-keeps-the-newest");
        T(StoryThreadPolicy.Extend("start", "next") == "start → next", "extend-joins");

        T(!StoryThreadPolicy.ShouldClose(StoryThreadPolicy.MaxSteps - 1) && StoryThreadPolicy.ShouldClose(StoryThreadPolicy.MaxSteps), "closes-at-max-steps");
        T(StoryThreadPolicy.IsExpired(0, StoryThreadPolicy.ExpireAfterTicks + 1) && !StoryThreadPolicy.IsExpired(0, 100), "expires-after-silence");

        string talk = Read("TalkService.cs.src");
        T(talk.Contains("StoryThreadService.Capture(talkRequest, receivedResponses)"), "talks-are-captured");
        T(talk.Contains("StoryThreadService.DrainCaptures()"), "captures-applied-on-main-thread");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
