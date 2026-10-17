using System;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;
using Ustas.RimAI.Communication.Util;
using RimWorld;
using Verse;
using Cache = Ustas.RimAI.Communication.Data.Cache;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Who a pawn would talk to right now, and whether a pawn with nobody near may talk anyway.
/// A talk the pawns start themselves is aimed at someone: without a recipient the prompt names
/// nobody, and the model turned to the one reader it could always assume - the player.
/// Main thread only: it reads positions and rooms of the cached pawns.
/// </summary>
public static class ConversationPartner
{
    private static readonly Random Rng = new();

    /// <summary>The nearest humanlike within hearing who is free to answer, or null.</summary>
    public static Pawn NearestFor(Pawn pawn)
    {
        if (pawn == null || !pawn.Spawned || pawn.IsPlayer()) return null;
        foreach (var other in PawnSelector.GetNearByTalkablePawns(pawn))
        {
            if (other == null || other.IsPlayer() || other.RaceProps?.Humanlike != true) continue;
            if (other.HostileTo(pawn)) continue;
            return other;
        }
        return null;
    }

    /// <summary>
    /// Gives a request that names nobody, or the speaker itself, the partner as its recipient.
    /// A recipient the request already names is kept.
    /// </summary>
    public static void Aim(TalkRequest request, Pawn partner)
    {
        if (request == null) return;
        if (request.Recipient == request.Initiator) request.Recipient = null;
        if (request.Recipient == null && partner != null && partner != request.Initiator)
            request.Recipient = partner;
    }

    /// <summary>
    /// Whether this turn may be spent by a pawn with nobody to talk to: a pawn in danger
    /// always may, otherwise only with the monologue chance.
    /// </summary>
    public static bool MayTalkAlone(Pawn pawn)
    {
        var settings = Settings.Get();
        if (!settings.AllowMonologue) return false;
        if (pawn.IsInDanger(true)) return true;
        return MonologuePolicy.Admit(settings.AllowMonologue, settings.MonologueChance, Rng.NextDouble());
    }
}
