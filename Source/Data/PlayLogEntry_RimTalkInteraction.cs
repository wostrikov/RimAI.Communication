using System.Collections.Generic;
using RimWorld;
using Verse;
using Ustas.RimAI.Communication.Service;

namespace Ustas.RimAI.Communication;

public class PlayLogEntry_RimTalkInteraction : PlayLogEntry_Interaction
{
    private string _cachedString;

    public PlayLogEntry_RimTalkInteraction()
    {
        // Parameterless constructor required for Scribing (loading from save)
    }

    public PlayLogEntry_RimTalkInteraction(
        InteractionDef interactionDef,
        Pawn initiator,
        Pawn recipient,
        List<RulePackDef> rules)
        : base(interactionDef, initiator, recipient, rules)
    {
        _cachedString = TalkService.GetTalk(initiator);
    }

    public Pawn Initiator => initiator;
    public Pawn Recipient => recipient;
    public List<RulePackDef> ExtraSentencePacks => extraSentencePacks;
    public string CachedString => _cachedString;
    public int TicksAbs => ticksAbs;

    /// <summary>
    /// The spoken line is the entry. It was never written to the save, so every
    /// RimAI talk in the play log came back blank after a reload - the
    /// parameterless constructor Scribe uses cannot call TalkService, and
    /// nothing else set the field.
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _cachedString, "rimAiTalkText");
    }

    // Override this method to customize the log message
    protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
    {
        if (!string.IsNullOrEmpty(_cachedString))
        {
            return _cachedString;
        }

        // An entry from a save written before the text was persisted. Falling
        // back to the interaction's own rules renders something rather than an
        // empty line - but those rules name the initiator, so a pawn the save
        // no longer holds (K038) would leave the resolver without it.
        if (initiator == null || recipient == null)
        {
            return string.Empty;
        }

        return base.ToGameStringFromPOV_Worker(pov, forceLog);
    }
}