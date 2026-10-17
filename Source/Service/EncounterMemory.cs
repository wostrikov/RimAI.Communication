using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Threading;
using Verse;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Which pawns have talked to each other in this save. Thoughts about a talk expire within days,
/// and a visitor whose thoughts had run out was introduced again at every talk.
/// </summary>
public static class EncounterMemory
{
    private static MetPairRegistry Registry => Find.World?.GetComponent<RimTalkWorldComponent>()?.MetPairs;

    /// <summary>Main thread only.</summary>
    public static bool HaveMet(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;
        return Registry?.HasMet(a.thingIDNumber, b.thingIDNumber) == true;
    }

    /// <summary>
    /// Called on the background thread once a talk is complete: everyone who took part has met
    /// everyone else. Thing ids are read here; the registry is reached on the main thread.
    /// </summary>
    public static void RecordTalk(TalkRequest request, List<TalkResponse> responses)
    {
        if (request == null || request.IsMonologue || responses == null || responses.Count == 0) return;

        var ids = new HashSet<int>();
        foreach (var pawn in responses.Select(r => r.SpeakerPawn).Append(request.Initiator).Append(request.Recipient))
        {
            if (pawn != null && !pawn.IsPlayer())
                ids.Add(pawn.thingIDNumber);
        }

        if (ids.Count < 2) return;
        var list = ids.ToList();
        MainThreadSchedulerAccess.RunInlineOrEnqueue(() =>
        {
            var registry = Registry;
            if (registry == null) return;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    registry.Record(list[i], list[j]);
        });
    }
}
