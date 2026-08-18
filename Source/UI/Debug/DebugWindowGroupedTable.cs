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

internal sealed class DebugWindowGroupedTable : DebugWindowCollaborator
{
    internal DebugWindowGroupedTable(DebugWindow owner) : base(owner) { }
    internal void DrawGroupedPawnTable(Rect rect)
    {
        if (Owner._pawnStates == null || !Owner._pawnStates.Any())
            return;

        float viewWidth = rect.width - 16f;
        float totalHeight = CalculateGroupedTableHeight(viewWidth);
        var viewRect = new Rect(0, 0, viewWidth, totalHeight);

        // Auto-scroll logic (Using main Owner._tableScrollPosition)
        float maxScroll = Mathf.Max(0f, totalHeight - rect.height);
        if (Owner._stickToBottom)
            Owner._tableScrollPosition.y = maxScroll;

        Widgets.BeginScrollView(rect, ref Owner._tableScrollPosition, viewRect);

        // Unstick if user manually scrolls up
        if (Owner._stickToBottom && Owner._tableScrollPosition.y < maxScroll - 1f)
            Owner._stickToBottom = false;

        float responseColumnWidth = CalculateGroupedResponseColumnWidth(viewRect.width);

        DrawGroupedHeader(new Rect(0, 0, viewRect.width, DebugWindow.HeaderHeight), responseColumnWidth);
        float currentY = DebugWindow.HeaderHeight;

        var sortedPawns = GetSortedPawnStates().ToList();

        // Filters
        if (!string.IsNullOrWhiteSpace(Owner._pawnFilter))
        {
            var needle = Owner._pawnFilter.Trim();
            sortedPawns = sortedPawns.Where(p =>
                (p.Pawn.LabelShort ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (!string.IsNullOrWhiteSpace(Owner._textSearch))
        {
            var needle = Owner._textSearch.Trim();
            sortedPawns = sortedPawns.Where(p =>
                    GetLastResponseForPawn(p.Pawn.LabelShort).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        for (int i = 0; i < sortedPawns.Count; i++)
        {
            var pawnState = sortedPawns[i];
            string pawnKey = pawnState.Pawn.LabelShort;
            bool isExpanded = Owner._expandedPawns.Contains(pawnKey);

            var rowRect = new Rect(0, currentY, viewRect.width, DebugWindow.RowHeight);
            if (i % 2 == 0) Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f, 0.4f));

            float currentX = 0;
            Widgets.Label(new Rect(rowRect.x + 5, rowRect.y + 3, 15, 15), isExpanded ? "-" : "+");
            currentX += DebugWindow.GroupedExpandIconWidth;

            var pawnNameRect = new Rect(currentX, rowRect.y, DebugWindow.GroupedPawnNameWidth, DebugWindow.RowHeight);
            UIUtil.DrawClickablePawnName(pawnNameRect, pawnKey, pawnState.Pawn);
            currentX += DebugWindow.GroupedPawnNameWidth + DebugWindow.ColumnPadding;

            string lastResponse = GetLastResponseForPawn(pawnKey);
            Widgets.Label(new Rect(currentX, rowRect.y, responseColumnWidth, DebugWindow.RowHeight), lastResponse);
            currentX += responseColumnWidth + DebugWindow.ColumnPadding;

            bool canTalk = pawnState.CanGenerateTalk();
            string statusText = canTalk
                ? "RimTalk.DebugWindow.StatusReady".Translate()
                : "RimTalk.DebugWindow.StatusBusy".Translate();
            GUI.color = canTalk ? Color.green : Color.yellow;
            Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.GroupedStatusWidth, DebugWindow.RowHeight), statusText);
            GUI.color = Color.white;
            currentX += DebugWindow.GroupedStatusWidth + DebugWindow.ColumnPadding;

            Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.GroupedLastTalkWidth, DebugWindow.RowHeight),
                pawnState.LastTalkTick.ToString());
            currentX += DebugWindow.GroupedLastTalkWidth + DebugWindow.ColumnPadding;

            Owner._talkLogsByPawn.TryGetValue(pawnKey, out var pawnRequests);
            var initiatingRequests = pawnRequests?.Where(r => r.IsFirstDialogue).ToList();
            Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.GroupedRequestsWidth, DebugWindow.RowHeight),
                (initiatingRequests?.Count ?? 0).ToString());
            currentX += DebugWindow.GroupedRequestsWidth + DebugWindow.ColumnPadding;

            Widgets.Label(new Rect(currentX, rowRect.y, DebugWindow.GroupedChattinessWidth, DebugWindow.RowHeight),
                pawnState.TalkInitiationWeight.ToString("F2"));

            if (Widgets.ButtonInvisible(rowRect))
            {
                if (isExpanded) Owner._expandedPawns.Remove(pawnKey);
                else Owner._expandedPawns.Add(pawnKey);
            }

            currentY += DebugWindow.RowHeight;

            // Expanded Inner List Logic
            if (isExpanded && Owner._talkLogsByPawn.TryGetValue(pawnKey, out var requests) && requests.Any())
            {
                const float indentWidth = 20f;
                float innerWidth = viewRect.width - indentWidth;
                float innerResponseWidth = Parts.Console.CalculateResponseColumnWidth(innerWidth, false);

                Parts.Console.DrawRequestTableHeader(new Rect(indentWidth, currentY, innerWidth, DebugWindow.HeaderHeight), innerResponseWidth,
                    false);
                currentY += DebugWindow.HeaderHeight;

                foreach (var r in requests)
                {
                    Parts.Console.DrawRequestRow(r, 0, currentY, innerWidth, indentWidth, innerResponseWidth, false);
                    currentY += DebugWindow.RowHeight;
                }
            }
        }

        Widgets.EndScrollView();

        Parts.Console.DrawStickToBottomOverlay(rect, maxScroll, ref Owner._tableScrollPosition);
    }

    internal void DrawGroupedHeader(Rect rect, float responseColumnWidth)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.3f, 0.3f, 0.8f));
        Text.Font = GameFont.Tiny;
        GUI.color = Color.white;

        float currentX = DebugWindow.GroupedExpandIconWidth;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, DebugWindow.GroupedPawnNameWidth, rect.height), "RimTalk.DebugWindow.HeaderPawn", "Pawn");
        currentX += DebugWindow.GroupedPawnNameWidth + DebugWindow.ColumnPadding;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, responseColumnWidth, rect.height), "RimTalk.DebugWindow.HeaderResponse", "Response");
        currentX += responseColumnWidth + DebugWindow.ColumnPadding;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, DebugWindow.GroupedStatusWidth, rect.height), "RimTalk.DebugWindow.HeaderStatus", "Status");
        currentX += DebugWindow.GroupedStatusWidth + DebugWindow.ColumnPadding;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, DebugWindow.GroupedLastTalkWidth, rect.height), "RimTalk.DebugWindow.HeaderLastTalk", "Last Talk");
        currentX += DebugWindow.GroupedLastTalkWidth + DebugWindow.ColumnPadding;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, DebugWindow.GroupedRequestsWidth, rect.height), "RimTalk.DebugWindow.HeaderRequests", "Requests");
        currentX += DebugWindow.GroupedRequestsWidth + DebugWindow.ColumnPadding;
        Parts.Console.DrawSortableHeader(new Rect(currentX, rect.y, DebugWindow.GroupedChattinessWidth, rect.height), "RimTalk.DebugWindow.HeaderChattiness", "Chattiness");
    }
    internal IEnumerable<PawnState> GetSortedPawnStates()
    {
        switch (Owner._sortColumn)
        {
            case "RimTalk.DebugWindow.HeaderPawn":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p => p.Pawn.LabelShort)
                    : Owner._pawnStates.OrderByDescending(p => p.Pawn.LabelShort);
        
            case "RimTalk.DebugWindow.HeaderRequests":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p =>
                        Owner._talkLogsByPawn.TryGetValue(p.Pawn.LabelShort, out var logs) 
                            ? logs.Count(r => r.IsFirstDialogue) : 0)
                    : Owner._pawnStates.OrderByDescending(p =>
                        Owner._talkLogsByPawn.TryGetValue(p.Pawn.LabelShort, out var logs) 
                            ? logs.Count(r => r.IsFirstDialogue) : 0);

            case "RimTalk.DebugWindow.HeaderResponse":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p => GetLastResponseForPawn(p.Pawn.LabelShort))
                    : Owner._pawnStates.OrderByDescending(p => GetLastResponseForPawn(p.Pawn.LabelShort));
        
            case "RimTalk.DebugWindow.HeaderStatus":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p => p.CanDisplayTalk())
                    : Owner._pawnStates.OrderByDescending(p => p.CanDisplayTalk());
        
            case "RimTalk.DebugWindow.HeaderLastTalk":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p => p.LastTalkTick)
                    : Owner._pawnStates.OrderByDescending(p => p.LastTalkTick);
        
            case "RimTalk.DebugWindow.HeaderChattiness":
                return Owner._sortAscending
                    ? Owner._pawnStates.OrderBy(p => p.TalkInitiationWeight)
                    : Owner._pawnStates.OrderByDescending(p => p.TalkInitiationWeight);
        
            default:
                return Owner._pawnStates;
        }
    }

    internal float CalculateGroupedResponseColumnWidth(float totalWidth)
    {
        float fixedWidth = DebugWindow.GroupedExpandIconWidth + DebugWindow.GroupedPawnNameWidth + DebugWindow.GroupedRequestsWidth + DebugWindow.GroupedLastTalkWidth +
                           DebugWindow.GroupedChattinessWidth + DebugWindow.GroupedStatusWidth;
        int columnGaps = 6;
        float availableWidth = totalWidth - fixedWidth - (DebugWindow.ColumnPadding * columnGaps);
        return Math.Max(150f, availableWidth);
    }

    internal float CalculateGroupedTableHeight(float viewWidth)
    {
        float height = DebugWindow.HeaderHeight + (Owner._pawnStates.Count * DebugWindow.RowHeight);
        foreach (var pawnState in Owner._pawnStates)
        {
            var pawnKey = pawnState.Pawn.LabelShort;
            if (Owner._expandedPawns.Contains(pawnKey) && Owner._talkLogsByPawn.TryGetValue(pawnKey, out var requests))
            {
                height += DebugWindow.HeaderHeight;
                height += requests.Count * DebugWindow.RowHeight;
            }
        }

        return height + 50f;
    }

    internal string GetLastResponseForPawn(string pawnKey)
    {
        if (Owner._talkLogsByPawn.TryGetValue(pawnKey, out var logs) && logs.Any())
        {
            return logs.Last().Response ?? Owner._generating;
        }

        return "";
    }
}
