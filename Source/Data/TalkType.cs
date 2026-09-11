namespace Ustas.RimAI.Communication.Data;

public enum TalkType
{
    Urgent,
    Hediff,
    LevelUp,
    Chitchat,
    Event,
    QuestOffer,
    QuestEnd,
    Thought,
    User,
    Announcement,
    Interaction,
    Sleep,
    Other
}

public static class TalkTypeExtensions
{
    public static bool IsFromUser(this TalkType talkType)
    {
        return talkType is TalkType.User or TalkType.Announcement;
    }

    /// <summary>Taken through the fast pool at once, rather than when the pawn's turn comes.</summary>
    public static bool IsFastTrack(this TalkType talkType)
    {
        return talkType is TalkType.User or TalkType.Announcement or TalkType.Interaction or TalkType.Urgent;
    }

    /// <summary>
    /// Whether a request may cancel the one being generated: the player's always may, and an
    /// interaction or an urgent line may cut into a background talk but not into each other.
    /// </summary>
    public static bool CanPreempt(this TalkType incomingType, TalkType currentType)
    {
        if (incomingType.IsFromUser()) return true;
        if (incomingType is TalkType.Interaction or TalkType.Urgent)
            return !currentType.IsFastTrack();
        return false;
    }
}