using System;
using System.IO;
using Ustas.RimAI.Communication.Policy;

internal static class TalkLifeStageTests
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

        T(TalkLifeStagePolicy.Classify(true, false) == TalkLifeStageKind.Baby, "classify-baby");
        T(TalkLifeStagePolicy.Classify(false, true) == TalkLifeStageKind.Child, "classify-child");
        T(TalkLifeStagePolicy.Classify(false, false) == TalkLifeStageKind.Adult, "classify-adult");
        T(TalkLifeStagePolicy.Classify(true, true) == TalkLifeStageKind.Baby, "classify-prechild-wins");

        T(TalkLifeStagePolicy.AllowsTalk(TalkLifeStageKind.Adult, false, false), "adult-always");
        T(TalkLifeStagePolicy.AllowsTalk(TalkLifeStageKind.Baby, true, false), "baby-allowed");
        T(!TalkLifeStagePolicy.AllowsTalk(TalkLifeStageKind.Baby, false, true), "baby-denied-even-if-child-on");
        T(TalkLifeStagePolicy.AllowsTalk(TalkLifeStageKind.Child, false, true), "child-allowed");
        T(!TalkLifeStagePolicy.AllowsTalk(TalkLifeStageKind.Child, true, false), "child-denied-even-if-baby-on");

        string pawn = Read("PawnUtil.cs.src");
        string settings = Read("CommunicationSettings.cs.src");
        string page = Read("CommunicationBasicSettingsPage.cs.src");
        T(pawn.Contains("TalkLifeStagePolicy.Classify"), "host-classify");
        T(pawn.Contains("TalkLifeStagePolicy.AllowsTalk"), "host-gate");
        T(pawn.Contains("IsChild()"), "host-child-stage");
        T(settings.Contains("AllowChildrenToTalk"), "settings-child-toggle");
        T(page.Contains("RimTalk.Settings.AllowChildrenToTalk"), "ui-child-toggle");
        T(page.Contains("RimTalk.Settings.AllowBabiesToTalk"), "ui-baby-toggle-still-distinct");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
