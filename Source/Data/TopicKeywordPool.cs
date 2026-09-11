namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// Short (one or two word) keywords a talk may be nudged with. Short keywords leave the model
/// room to phrase things freshly, and none of them contradicts a persona, the weather or DLC lore.
/// </summary>
public static class TopicKeywordPool
{
    /// <summary>The angle of a remark; works for a monologue or a conversation and any personality.</summary>
    public static readonly string[] ApproachKeywords =
    [
        "banter", "complaint", "nostalgia", "philosophy", "curiosity",
        "worry", "storytelling", "bragging", "rumor", "confession",
        "praise", "observation", "ambition", "debate", "advice",
        "sarcasm", "sympathy", "daydreaming", "reassurance", "speculation",
        "boredom", "self-mockery", "relief", "caution", "skepticism",
        "gratitude", "hesitation", "enthusiasm", "resignation", "morbid humor",
        "envy", "awkwardness", "determination", "suspicion", "shyness",
        "fascination", "fondness", "cynicism", "impatience", "indifference",
        "provocation", "regret", "lightheartedness", "melancholy", "amazement",
        "earnestness", "irritation", "playfulness", "solemnity", "yearning"
    ];

    /// <summary>
    /// Subjects that stand on their own - the past, tastes, inner life, values, frontier lore -
    /// and so never assert a fact about what is happening on the map right now.
    /// </summary>
    public static readonly string[] SubjectKeywords =
    [
        // Past life and origins
        "childhood", "hometown", "past job", "family memories", "old mentors",
        "forgotten skills", "school days", "past mistakes", "first journey", "family heirlooms",
        "childhood games", "lost keepsakes", "cryptosleep stories", "accent and dialect",
        "past celebrations", "earliest memory", "life before landing", "old friends",

        // Tastes, habits and quirks
        "taste in music", "favorite flavors", "bad habits", "useless skills", "meaning of names",
        "superstitions", "personal rituals", "hidden talents", "things people misunderstand", "sense of humor",
        "pet peeves", "definition of home", "awkward memories", "guilty pleasures", "personal pride",

        // Inner mind
        "trust", "secrets", "loyalty", "forgiveness", "loneliness",
        "guilt and regrets", "stubbornness", "patience", "fears", "peace of mind",

        // Values and the future
        "future dreams", "retirement dreams", "luck and fate", "fate vs choice", "meaning of survival",
        "what comes next", "value of money", "fear of aging", "hope", "human nature",
        "legacy", "good luck charms", "second chances", "justice", "curiosity about space",

        // Legends and frontier rumors
        "glitterworlds", "ancient legends", "tribal myths", "old earth tales", "space travel stories",
        "bionic philosophy", "drifter stories", "strange rumors", "survival wisdom", "deep space myths",
        "frontier legends", "lost colony rumors"
    ];
}
