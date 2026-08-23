namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// Baby versus child talk gates. Babies are pre-Child life stages;
/// children are the Child stage only. Adults are never gated here.
/// </summary>
public enum TalkLifeStageKind
{
    Adult = 0,
    Child = 1,
    Baby = 2,
}

public static class TalkLifeStagePolicy
{
    public static TalkLifeStageKind Classify(bool isPreChild, bool isChildStage)
    {
        if (isPreChild)
            return TalkLifeStageKind.Baby;
        if (isChildStage)
            return TalkLifeStageKind.Child;
        return TalkLifeStageKind.Adult;
    }

    public static bool AllowsTalk(TalkLifeStageKind stage, bool allowBabies, bool allowChildren)
    {
        switch (stage)
        {
            case TalkLifeStageKind.Baby:
                return allowBabies;
            case TalkLifeStageKind.Child:
                return allowChildren;
            default:
                return true;
        }
    }
}
