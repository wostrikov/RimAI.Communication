using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Service;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Cache = Ustas.RimAI.Communication.Data.Cache;
using State = Ustas.RimAI.Communication.Data.ApiLog.State;

namespace Ustas.RimAI.Communication.UI;

internal sealed class DebugWindowConsoleTable : DebugWindowCollaborator
{
    internal DebugWindowConsoleTable(DebugWindow owner) : base(owner) { }
    internal void DrawConsoleTable(Rect rect)
    {
        // Column Headers
        float responseWidth = CalculateResponseColumnWidth(rect.width, true);
        DrawRequestTableHeader(new Rect(rect.x, rect.y, rect.width, DebugWindow.HeaderHeight), responseWidth, true);

        // Scroll View
        var scrollRect = new Rect(rect.x, rect.y + DebugWindow.HeaderHeight, rect.width, rect.height - DebugWindow.HeaderHeight);
        float viewWidth = scrollRect.width - 16f;
        float viewHeight = Owner._requests.Count * DebugWindow.RowHeight;
        var viewRect = new Rect(0, 0, viewWidth, viewHeight);

        // Calculate max scroll
        float maxScroll = Mathf.Max(0f, viewHeight - scrollRect.height);

        if (Owner._stickToBottom)
            Owner._tableScrollPosition.y = maxScroll;

        Widgets.BeginScrollView(scrollRect, ref Owner._tableScrollPosition, viewRect);

        if (Owner._stickToBottom && Owner._tableScrollPosition.y < maxScroll - 1f)
            Owner._stickToBottom = false;

        // Determine blocked area for input
        const float btnSize = 30f;
        Rect? overlayContentRect = null;
        if (!Owner._stickToBottom)
        {
            float overlayWinX = scrollRect.xMax - btnSize - 20f;
            float overlayWinY = scrollRect.yMax - btnSize - 5f;
            float overlayContentX = overlayWinX - scrollRect.x + Owner._tableScrollPosition.x;
            float overlayContentY = overlayWinY - scrollRect.y + Owner._tableScrollPosition.y;
            overlayContentRect = new Rect(overlayContentX, overlayContentY, btnSize, btnSize);
        }

        // Virtualization
        float visibleTop = Owner._tableScrollPosition.y;
        float visibleBottom = Owner._tableScrollPosition.y + scrollRect.height;
        int firstIndex = Mathf.Clamp((int)(visibleTop / DebugWindow.RowHeight), 0, Owner._requests.Count);
        int lastIndex = Mathf.Clamp((int)(visibleBottom / DebugWindow.RowHeight) + 1, 0, Owner._requests.Count);

        for (int i = firstIndex; i < lastIndex; i++)
        {
            float rowY = i * DebugWindow.RowHeight;
            bool inputBlocked = overlayContentRect.HasValue && Mouse.IsOver(overlayContentRect.Value);
            DrawRequestRow(Owner._requests[i], i, rowY, viewWidth, 0f, responseWidth, true, inputBlocked);
        }

        Widgets.EndScrollView();

        // Use Helper Method
        DrawStickToBottomOverlay(scrollRect, maxScroll, ref Owner._tableScrollPosition);
    }

    internal void DrawRequestRow(ApiLog request, int rowIndex, float rowY, float totalWidth, float xOffset,
        float responseColumnWidth, bool showPawnColumn, bool inputBlocked = false)
    {
        var rowRect = new Rect(xOffset, rowY, totalWidth, DebugWindow.RowHeight);
        if (rowIndex % 2 == 0) Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f, 0.4f));

        bool isSelected = Owner._selectedLog != null && Owner._selectedLog.Id == request.Id;
        if (isSelected) Widgets.DrawBoxSolid(rowRect, new Color(0.2f, 0.25f, 0.35f, 0.45f));

        float currentX = xOffset + 5f;
        Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.TimestampColumnWidth, DebugWindow.RowHeight),
            request.Timestamp.ToString("HH:mm:ss"));
        currentX += DebugWindow.TimestampColumnWidth + DebugWindow.ColumnPadding;

        if (showPawnColumn)
        {
            string pawnName = request.Name ?? "-";
            var pawnNameRect = new Rect(currentX, rowRect.y, DebugWindow.PawnColumnWidth, DebugWindow.RowHeight);
            var pawn = Owner._pawnStates.FirstOrDefault(p => p.Pawn.LabelShort == pawnName)?.Pawn;
            UIUtil.DrawClickablePawnName(pawnNameRect, pawnName, pawn);
            currentX += DebugWindow.PawnColumnWidth + DebugWindow.ColumnPadding;
        }

        string resp = request.Response ?? Owner._generating;
        Text.WordWrap = false;
        Widgets.Label(new Rect(currentX, rowRect.y, responseColumnWidth, DebugWindow.RowHeight), resp);
        Text.WordWrap = true;
        currentX += responseColumnWidth + DebugWindow.ColumnPadding;

        string interactionType = request.InteractionType ?? "-";
        Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.InteractionTypeColumnWidth, DebugWindow.RowHeight), interactionType);
        currentX += DebugWindow.InteractionTypeColumnWidth + DebugWindow.ColumnPadding;

        string elapsedMsText = request.Response == null
            ? "" : request.ElapsedMs == 0 ? "-" : request.ElapsedMs.ToString();
        Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.TimeColumnWidth, DebugWindow.RowHeight), elapsedMsText);
        currentX += DebugWindow.TimeColumnWidth + DebugWindow.ColumnPadding;

        int count = request.Payload?.TokenCount ?? 0;
        string tokenCountText = count != 0
            ? count.ToString()
            : request.IsFirstDialogue ? "-" : "";
        Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.TokensColumnWidth, DebugWindow.RowHeight), tokenCountText);
        currentX += DebugWindow.TokensColumnWidth + DebugWindow.ColumnPadding;

        State stateFilter = request.GetState();
        GUI.color = stateFilter.GetColor();
        Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.StateColumnWidth, DebugWindow.RowHeight), stateFilter.GetLabel());
        GUI.color = Color.white;

        if (!inputBlocked)
        {
            TooltipHandler.TipRegion(rowRect, "RimTalk.DebugWindow.TooltipSelectForDetails".Translate());
        }

        if (!inputBlocked && Widgets.ButtonInvisible(rowRect))
        {
            Owner._selectedLog = request;
            // Interrupt auto-scroll on selection
            Owner._stickToBottom = false;
        }
    }
    internal void DrawRequestTableHeader(Rect rect, float responseColumnWidth, bool showPawnColumn)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.25f, 0.9f));
        Text.Font = GameFont.Tiny;
        float currentX = rect.x + 5f;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.TimestampColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderTimestamp".Translate());
        currentX += DebugWindow.TimestampColumnWidth + DebugWindow.ColumnPadding;
        if (showPawnColumn)
        {
            Widgets.Label(new Rect(currentX, rect.y, DebugWindow.PawnColumnWidth, rect.height),
                "RimTalk.DebugWindow.HeaderPawn".Translate());
            currentX += DebugWindow.PawnColumnWidth + DebugWindow.ColumnPadding;
        }

        Widgets.Label(new Rect(currentX, rect.y, responseColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderResponse".Translate());
        currentX += responseColumnWidth + DebugWindow.ColumnPadding;
        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.InteractionTypeColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderType".Translate());
        currentX += DebugWindow.InteractionTypeColumnWidth + DebugWindow.ColumnPadding;
        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.TimeColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderTimeMs".Translate());
        currentX += DebugWindow.TimeColumnWidth + DebugWindow.ColumnPadding;
        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.TokensColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderTokens".Translate());
        currentX += DebugWindow.TokensColumnWidth + DebugWindow.ColumnPadding;
        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.StateColumnWidth, rect.height),
            "RimTalk.DebugWindow.HeaderState".Translate());
    }

    internal void DrawStickToBottomOverlay(Rect scrollRect, float maxScroll, ref Vector2 scrollPosition)
    {
        if (Owner._stickToBottom) return;

        const float btnSize = 30f;
        // Position the button inside the scroll rect, bottom-right
        var overlayRect = new Rect(scrollRect.xMax - btnSize - 20f, scrollRect.yMax - btnSize - 5f, btnSize, btnSize);

        bool isMouseOver = Mouse.IsOver(overlayRect);
        Color bgColor = isMouseOver
            ? new Color(0.3f, 0.3f, 0.3f, 1f)
            : new Color(0, 0, 0, 0.6f);

        Widgets.DrawBoxSolid(overlayRect, bgColor);

        if (Widgets.ButtonInvisible(overlayRect))
        {
            Owner._stickToBottom = true;
            scrollPosition.y = maxScroll;
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;

        var labelRect = overlayRect;
        labelRect.xMin += 5f;

        Widgets.Label(labelRect, "▼");

        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Tiny;

        TooltipHandler.TipRegion(overlayRect, "RimTalk.DebugWindow.AutoScroll".Translate());
    }

    internal void DrawSortableHeader(Rect rect, string key, string defaultLabel)
    {
        string translatedColumn = key.Translate();
        if (translatedColumn == key) translatedColumn = defaultLabel; // Fallback if key missing
        
        string arrow = Owner._sortColumn == key ? (Owner._sortAscending ? " ▲" : " ▼") : "";
        if (Widgets.ButtonInvisible(rect))
        {
            if (Owner._sortColumn == key) Owner._sortAscending = !Owner._sortAscending;
            else
            {
                Owner._sortColumn = key;
                Owner._sortAscending = true;
            }

            var settings = Settings.Get();
            settings.DebugSortColumn = Owner._sortColumn;
            settings.DebugSortAscending = Owner._sortAscending;
        }

        Widgets.Label(rect, translatedColumn + arrow);
    }

    internal float CalculateResponseColumnWidth(float totalWidth, bool includePawnColumn)
    {
        float fixedWidth = DebugWindow.TimestampColumnWidth + DebugWindow.TimeColumnWidth + DebugWindow.TokensColumnWidth + DebugWindow.StateColumnWidth +
                           DebugWindow.InteractionTypeColumnWidth;
        int columnGaps = 6;
        if (includePawnColumn)
        {
            fixedWidth += DebugWindow.PawnColumnWidth;
            columnGaps++;
        }

        float availableWidth = totalWidth - fixedWidth - (DebugWindow.ColumnPadding * columnGaps);
        return Mathf.Max(40f, availableWidth);
    }
}
