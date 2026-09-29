using System;
using System.Collections.Generic;
using System.Linq;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// What the model sent back as a pawn's standing orders, made fit to keep: a few short orders,
/// each once. Host-free, so it can be tested without the game.
/// </summary>
public static class PlayerOrderPolicy
{
    public const int MaxOrders = 5;
    public const int MaxOrderLength = 100;
    public const string Separator = "; ";

    private static readonly string[] ClearedWords = ["none", "null", "[]", "-", "n/a"];

    /// <summary>
    /// The orders to keep. Null when the model said nothing about orders, so the ones kept stay;
    /// empty when it withdrew them all.
    /// </summary>
    public static string Normalize(string raw)
    {
        if (raw == null) return null;

        var orders = new List<string>();
        foreach (var part in raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string order = part.Trim().Trim('"', '\'', '-', '*', '•', '[', ']').Trim();
            if (order.Length == 0 || ClearedWords.Contains(order, StringComparer.OrdinalIgnoreCase)) continue;
            order = Clip(order);
            if (!orders.Contains(order, StringComparer.OrdinalIgnoreCase)) orders.Add(order);
            if (orders.Count == MaxOrders) break;
        }

        return string.Join(Separator, orders);
    }

    private static string Clip(string order)
    {
        if (order.Length <= MaxOrderLength) return order;
        int cut = order.LastIndexOf(' ', MaxOrderLength);
        return order.Substring(0, cut > MaxOrderLength / 2 ? cut : MaxOrderLength).TrimEnd(',', '.', ' ');
    }
}
