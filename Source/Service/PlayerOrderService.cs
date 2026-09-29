using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Policy;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Orders the player gives a pawn in conversation - "keep watch at the gate", "you are the cook
/// now" - remembered with the save and put back into that pawn's profile in every later talk, so
/// the pawn does not forget them once the line is spoken. The model reports them: asked, in a
/// talk with the player, to send back the pawn's full set of orders whenever they change.
/// </summary>
public static class PlayerOrderService
{
    /// <summary>Pawns whose orders are kept; the ones left alone longest go first.</summary>
    private const int MaxPawns = 200;

    private static readonly ConcurrentQueue<(string pawnId, string orders)> Pending = new();

    private static List<PawnOrders> Store => Find.World?.GetComponent<RimTalkWorldComponent>()?.PlayerOrders;

    public static bool Enabled => Settings.Get().Context.IncludePlayerOrders;

    /// <summary>
    /// The pawn a talk may give orders to: the one the player speaks to. An announcement is to
    /// everyone and replaces nobody's own orders.
    /// </summary>
    public static Pawn RecipientOf(TalkRequest request)
    {
        if (!Enabled || request == null || request.IsAnnouncement || !request.TalkType.IsFromUser()) return null;
        var pawn = request.Participants?.FirstOrDefault();
        return pawn == null || pawn.IsPlayer() ? null : pawn;
    }

    /// <summary>What the model is asked to do with orders, in a talk where the player addresses a pawn.</summary>
    public static string Instruction(string pawnName, string speakerName) =>
        $"\nIf {speakerName} just gave {pawnName} an order, a rule to keep or a role, add \"orders\" to {pawnName}'s line: " +
        $"every order {pawnName} now follows, a few words each, separated by \"{PlayerOrderPolicy.Separator.Trim()}\" - " +
        "the standing ones included, less any the new words replace or cancel; \"orders\": \"\" if all were withdrawn. " +
        "Otherwise leave \"orders\" out.";

    /// <summary>The line in the pawn's profile, or null when it has no orders.</summary>
    public static string ContextLine(Pawn pawn)
    {
        if (!Enabled || pawn == null) return null;
        string orders = Get(pawn);
        return string.IsNullOrEmpty(orders)
            ? null
            : $"Standing orders from {Settings.Get().PlayerName}: {orders} (keeps to them in word and deed)";
    }

    /// <summary>The pawn's orders, or an empty string. Main thread only.</summary>
    public static string Get(Pawn pawn)
    {
        string id = pawn?.ThingID;
        return Store?.FirstOrDefault(o => o.PawnId == id)?.Orders ?? string.Empty;
    }

    /// <summary>Called from the streaming thread; applied by <see cref="DrainCaptures"/>.</summary>
    public static void Capture(Pawn pawn, string reportedOrders)
    {
        string orders = PlayerOrderPolicy.Normalize(reportedOrders);
        if (pawn == null || orders == null) return;
        Pending.Enqueue((pawn.ThingID, orders));
    }

    /// <summary>Main thread only.</summary>
    public static void DrainCaptures()
    {
        while (Pending.TryDequeue(out var item))
            Set(item.pawnId, item.orders);
    }

    /// <summary>Drops all of the pawn's orders. Main thread only.</summary>
    public static void Forget(Pawn pawn)
    {
        if (pawn != null) Set(pawn.ThingID, string.Empty);
    }

    private static void Set(string pawnId, string orders)
    {
        var store = Store;
        if (store == null || pawnId == null) return;

        store.RemoveAll(o => o.PawnId == pawnId);
        if (!string.IsNullOrEmpty(orders))
            store.Add(new PawnOrders { PawnId = pawnId, Orders = orders, Tick = GenTicks.TicksGame });
        while (store.Count > MaxPawns)
            store.Remove(store.OrderBy(o => o.Tick).First());

        Logger.Debug($"Orders for {pawnId}: {(string.IsNullOrEmpty(orders) ? "(none)" : orders)}");
    }
}
