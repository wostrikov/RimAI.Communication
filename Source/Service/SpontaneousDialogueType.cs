using System.Collections.Generic;
using System.Text;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// The dialogue type of a talk the pawns start themselves - not one the player asked for, and
/// not an announcement: who speaks, in what tone, and what about.
/// </summary>
public static class SpontaneousDialogueType
{
    public static void Build(StringBuilder intentSb, StringBuilder topicSb, TalkRequest talkRequest,
        List<Pawn> pawns, string shortName, Pawn mainPawn)
    {
        Pawn partner = pawns.Count > 1 ? (pawns[0] == mainPawn ? pawns[1] : pawns[0]) : null;
        bool brawling = mainPawn.IsBrawlingWithAlly(out var opponent);
        bool inCombat = brawling || mainPawn.IsInCombat() || mainPawn.GetMapRole() == MapRole.Invading;
        bool isFirstMeeting = !inCombat && DialogueTone.IsFirstMeeting(mainPawn, partner);

        // Combat first: a pawn fighting alone is in combat, not musing to itself.
        if (inCombat)
        {
            if (talkRequest.TalkType != TalkType.Urgent && !mainPawn.InMentalState)
                talkRequest.Prompt = null;

            talkRequest.TalkType = TalkType.Urgent;
            AppendCombatIntent(intentSb, mainPawn, pawns, shortName, brawling, opponent);
        }
        else if (pawns.Count == 1)
        {
            // Named as one speaker: "monologue" alone let the model write lines for pawns nearby.
            intentSb.Append($"{shortName} short monologue (only {shortName} speaks)");
        }
        else
        {
            intentSb.Append($"{shortName} starts conversation, taking turns");
        }

        if (!inCombat)
        {
            string tone = DialogueTone.TowardPatient(mainPawn, pawns)
                          ?? (partner != null ? DialogueTone.TowardPartner(mainPawn, partner, pawns, isFirstMeeting) : null);
            if (tone != null)
                intentSb.Append('\n').Append(tone);
        }

        if (mainPawn.InMentalState)
        {
            // Drawn once per request: this method runs twice per talk.
            if (!talkRequest.TopicHintDrawn)
            {
                talkRequest.TopicHintDrawn = true;
                talkRequest.TopicHint = TopicService.DrawFixation(talkRequest, mainPawn);
            }
            topicSb.Append(DialogueTone.MentalBreak(mainPawn, talkRequest.TopicHint));
        }
        else if (mainPawn.IsDownedInPain())
            topicSb.Append("(downed in pain. Short, strained dialogue)");
        else if (talkRequest.Prompt != null)
            topicSb.Append(talkRequest.Prompt);
        else if (talkRequest.TalkType != TalkType.Urgent && !isFirstMeeting)
        {
            // Without a prompt of its own, a talk gets a fresh angle, or a story to carry on,
            // which is what keeps the same pair from circling the same few remarks. A first
            // meeting gets none: two strangers introduce themselves before they reminisce.
            if (!talkRequest.TopicHintDrawn)
            {
                talkRequest.TopicHintDrawn = true;
                talkRequest.TopicHint = TopicService.DrawHint(talkRequest, mainPawn);
            }
            if (talkRequest.TopicHint != null)
                topicSb.Append(talkRequest.TopicHint);
        }
    }

    private static void AppendCombatIntent(StringBuilder intentSb, Pawn mainPawn, List<Pawn> pawns,
        string shortName, bool brawling, Pawn opponent)
    {
        if (mainPawn.IsFleeing())
            intentSb.Append($"{shortName} dialogue short, panicked tone (fleeing)");
        else if (brawling)
            intentSb.Append($"{shortName} dialogue short, angry and heated (brawling with {(opponent != null ? PromptService.GetUniqueName(opponent, pawns) : "someone")})");
        else if (mainPawn.IsSlave || mainPawn.IsPrisoner)
            intentSb.Append($"{shortName} dialogue short (worry)");
        else if (DialogueTone.TryDescribeCombatSides(mainPawn, pawns, out var sides))
            intentSb.Append($"{shortName} dialogue short, urgent combat tone\n{sides}");
        else
            intentSb.Append($"{shortName} dialogue short, urgent tone ({mainPawn.GetMapRole().ToString().ToLower()}/command)");
    }
}
