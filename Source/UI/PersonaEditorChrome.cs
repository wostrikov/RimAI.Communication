using System;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.UI;

/// <summary>
/// Optional Persona Editor footer chrome. Personas and Voices subscribe.
/// </summary>
public static class PersonaEditorChrome
{
    public static event Action<PersonaEditorWindow, Pawn, Rect> DrawFooter;
    public static event Func<PersonaEditorWindow, Pawn, bool> TryHandleRollGen;

    public static void PublishFooter(PersonaEditorWindow window, Pawn pawn, Rect inRect) =>
        DrawFooter?.Invoke(window, pawn, inRect);

    public static bool HandleRollGen(PersonaEditorWindow window, Pawn pawn)
    {
        var handlers = TryHandleRollGen;
        if (handlers == null)
            return false;
        foreach (Func<PersonaEditorWindow, Pawn, bool> handler in handlers.GetInvocationList())
        {
            if (handler(window, pawn))
                return true;
        }

        return false;
    }
}
