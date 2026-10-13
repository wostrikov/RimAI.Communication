using System;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Where the cursor of a multi-line text area is, for scrolling the area to
/// keep it in view while the player types.
///
/// Unity's TextEditor answered that itself with graphicalCursorPos. The engine
/// RimWorld 1.7 runs on keeps text layout in its GUI backend, and its own text
/// area asks the backend where to draw the caret. This asks the same question
/// with the same font and wrap width, so the answer is the line the caret is
/// drawn on and not a second measurement that could put a long word on the
/// line before the one it wrapped to.
/// </summary>
internal static class TextAreaCursor
{
    /// <summary>
    /// The top of the line the cursor is on, from the top of a text area
    /// <paramref name="width"/> wide drawn in the current text area style.
    /// </summary>
    public static float LineTop(string text, int cursorIndex, float width)
    {
        if (string.IsNullOrEmpty(text) || cursorIndex <= 0)
            return 0f;

        GUIStyle style = Text.CurTextAreaStyle;
        RectOffset padding = style.padding ?? new RectOffset();
        IGuiBackend backend = GuiBackend.Current;
        if (backend == null)
            return padding.top;

        float wrapWidth = style.wordWrap ? Mathf.Max(0f, width - padding.left - padding.right) : -1f;
        Vector2 cursor = backend.TextCursorPos(
            style.FontIndex,
            0f,
            FontStyle.Normal,
            text,
            wrapWidth,
            Math.Min(cursorIndex, text.Length),
            out _);
        return Mathf.Max(0f, padding.top + cursor.y);
    }
}
