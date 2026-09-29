using Ustas.RimAI.Communication.Service;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.UI;

/// <summary>
/// One row in the persona editor: the orders the player gave this pawn in conversation, and a
/// button to make it forget them.
/// </summary>
public static class PlayerOrdersRow
{
    private const float ButtonWidth = 110f;

    public static void Draw(Rect rect, Pawn pawn)
    {
        string orders = PlayerOrderService.Get(pawn);
        bool hasOrders = !string.IsNullOrEmpty(orders);

        Rect labelRect = hasOrders ? new Rect(rect.x, rect.y, rect.width - ButtonWidth - 8f, rect.height) : rect;
        string label = hasOrders
            ? "RimTalk.PersonaEditor.Orders".Translate(orders).ToString()
            : "RimTalk.PersonaEditor.NoOrders".Translate().ToString();

        var anchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = hasOrders ? Color.white : Color.gray;
        Widgets.Label(labelRect, label.Truncate(labelRect.width));
        GUI.color = Color.white;
        Text.Anchor = anchor;
        string explanation = "RimTalk.PersonaEditor.Orders.Tooltip".Translate();
        TooltipHandler.TipRegion(labelRect, hasOrders ? $"{label}\n\n{explanation}" : explanation);

        if (hasOrders && Widgets.ButtonText(new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, rect.height),
                "RimTalk.PersonaEditor.ForgetOrders".Translate()))
            PlayerOrderService.Forget(pawn);
    }
}
