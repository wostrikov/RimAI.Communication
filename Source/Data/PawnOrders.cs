using Verse;

namespace Ustas.RimAI.Communication.Data;

/// <summary>The orders the player gave one pawn in conversation. Saved with the world.</summary>
public class PawnOrders : IExposable
{
    public string PawnId;   // ThingID
    public string Orders;   // "order; order; order"
    public int Tick;

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref Orders, "orders");
        Scribe_Values.Look(ref Tick, "tick");
    }
}
