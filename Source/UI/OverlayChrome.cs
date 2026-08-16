using System;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.UI;

/// <summary>
/// Optional overlay chrome drawn beside the Communication gear icon.
/// Voices subscribes; Communication does not reference sibling modules.
/// </summary>
public static class OverlayChrome
{
    public static event Action<Rect> DrawAdjacentToGear;
    public static event Func<Event, bool> TryConsumeClick;

    public static void Draw(Rect gearIconScreenRect) => DrawAdjacentToGear?.Invoke(gearIconScreenRect);

    public static bool ConsumeClick(Event current)
    {
        var gate = TryConsumeClick;
        if (gate == null || current == null)
            return false;
        foreach (Func<Event, bool> handler in gate.GetInvocationList())
        {
            if (handler(current))
                return true;
        }

        return false;
    }
}
