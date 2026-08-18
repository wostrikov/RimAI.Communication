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

internal sealed class DebugWindowActiveRequests : DebugWindowCollaborator
{
    internal DebugWindowActiveRequests(DebugWindow owner) : base(owner) { }
    internal void DrawActiveRequestsTable(Rect rect)
    {
        // Header
        float fixedWidth = DebugWindow.ARTimeWidth + DebugWindow.ARInitiatorWidth + DebugWindow.ARRecipientWidth + DebugWindow.ARTypeWidth + DebugWindow.ARElapsedWidth +
                           DebugWindow.ARStatusWidth + (DebugWindow.ColumnPadding * 6);
        float promptWidth = Mathf.Max(50f, rect.width - fixedWidth - 16f);

        DrawActiveRequestHeader(new Rect(rect.x, rect.y, rect.width, DebugWindow.HeaderHeight), promptWidth);

        var scrollRect = new Rect(rect.x, rect.y + DebugWindow.HeaderHeight, rect.width, rect.height - DebugWindow.HeaderHeight);
        float viewWidth = scrollRect.width - 16f;
        float viewHeight = Owner._cachedActiveViewList.Count * DebugWindow.RowHeight;
        var viewRect = new Rect(0, 0, viewWidth, viewHeight);

        // Auto-scroll logic
        float maxScroll = Mathf.Max(0f, viewHeight - scrollRect.height);
        if (Owner._stickToBottom)
            Owner._activeRequestsScrollPosition.y = maxScroll;

        Widgets.BeginScrollView(scrollRect, ref Owner._activeRequestsScrollPosition, viewRect);

        // Unstick if user manually scrolls up
        if (Owner._stickToBottom && Owner._activeRequestsScrollPosition.y < maxScroll - 1f)
            Owner._stickToBottom = false;

        // Iterate the cached list
        for (int i = 0; i < Owner._cachedActiveViewList.Count; i++)
        {
            DrawActiveRequestRow(Owner._cachedActiveViewList[i], i, i * DebugWindow.RowHeight, viewWidth, promptWidth);
        }

        Widgets.EndScrollView();

        Parts.Console.DrawStickToBottomOverlay(scrollRect, maxScroll, ref Owner._activeRequestsScrollPosition);
    }

    internal void DrawActiveRequestHeader(Rect rect, float promptWidth)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.25f, 0.9f));
        Text.Font = GameFont.Tiny;
        float currentX = rect.x + 5f;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARTimeWidth, rect.height),
            "RimTalk.DebugWindow.HeaderTimestamp".Translate());
        currentX += DebugWindow.ARTimeWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARInitiatorWidth, rect.height),
            "RimTalk.DebugWindow.HeaderInitiator".Translate());
        currentX += DebugWindow.ARInitiatorWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARRecipientWidth, rect.height),
            "RimTalk.DebugWindow.HeaderRecipient".Translate());
        currentX += DebugWindow.ARRecipientWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, promptWidth, rect.height),
            "RimTalk.DebugWindow.HeaderPrompt".Translate());
        currentX += promptWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARTypeWidth, rect.height),
            "RimTalk.DebugWindow.HeaderType".Translate());
        currentX += DebugWindow.ARTypeWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARElapsedWidth, rect.height),
            "RimTalk.DebugWindow.HeaderElapsed".Translate());
        currentX += DebugWindow.ARElapsedWidth + DebugWindow.ColumnPadding;

        Widgets.Label(new Rect(currentX, rect.y, DebugWindow.ARStatusWidth, rect.height),
            "RimTalk.DebugWindow.HeaderStatus".Translate());
    }

    internal void DrawActiveRequestRow(TalkRequest req, int index, float rowY, float width, float promptWidth)
    {
        var rowRect = new Rect(0, rowY, width, DebugWindow.RowHeight);

        if (index % 2 == 0) Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f, 0.4f));

        float currentX = 5f;
        int currentTick = GenTicks.TicksGame;

        // 1. Time (HH:mm:ss to match main table)
        Widgets.Label(new Rect(currentX, rowY, DebugWindow.ARTimeWidth, DebugWindow.RowHeight), req.CreatedTime.ToString("HH:mm:ss"));
        currentX += DebugWindow.ARTimeWidth + DebugWindow.ColumnPadding;

        // 2. Initiator
        var initRect = new Rect(currentX, rowY, DebugWindow.ARInitiatorWidth, DebugWindow.RowHeight);
        string initName = req.Initiator?.LabelShort ?? "-";
        UIUtil.DrawClickablePawnName(initRect, initName, req.Initiator);
        currentX += DebugWindow.ARInitiatorWidth + DebugWindow.ColumnPadding;

        // 3. Recipient
        var recRect = new Rect(currentX, rowY, DebugWindow.ARRecipientWidth, DebugWindow.RowHeight);
        if (req.Recipient != null && req.Recipient != req.Initiator)
            UIUtil.DrawClickablePawnName(recRect, req.Recipient.LabelShort, req.Recipient);
        else
            Widgets.Label(recRect, "-");
        currentX += DebugWindow.ARRecipientWidth + DebugWindow.ColumnPadding;

        // 4. Prompt (Truncated)
        string prompt = req.Prompt ?? "";
        Text.WordWrap = false;
        Widgets.Label(new Rect(currentX, rowY, promptWidth, DebugWindow.RowHeight), prompt);
        Text.WordWrap = true;
        currentX += promptWidth + DebugWindow.ColumnPadding;

        // 5. Type
        Widgets.Label(new Rect(currentX, rowY, DebugWindow.ARTypeWidth, DebugWindow.RowHeight), req.TalkType.ToString());
        currentX += DebugWindow.ARTypeWidth + DebugWindow.ColumnPadding;

        // 6. Elapsed
        int ticksElapsed = Math.Max(0,
            (req.Status == RequestStatus.Pending || req.FinishedTick == -1 ? GenTicks.TicksGame : req.FinishedTick) -
            req.CreatedTick);
        string elapsedStr = $"{ticksElapsed / 60}s";
        Widgets.Label(new Rect(currentX, rowY, DebugWindow.ARElapsedWidth, DebugWindow.RowHeight), elapsedStr);
        currentX += DebugWindow.ARElapsedWidth + DebugWindow.ColumnPadding;

        // 7. Status
        Color c = GUI.color;
        if (req.Status == RequestStatus.Expired) GUI.color = Color.gray;
        else if (req.Status == RequestStatus.Processed) GUI.color = Color.green;
        else GUI.color = Color.yellow;

        string translationKey = $"RimTalk.DebugWindow.State{req.Status}";
        Widgets.Label(new Rect(currentX, rowY, DebugWindow.ARStatusWidth, DebugWindow.RowHeight), translationKey.Translate());
        GUI.color = c;
    }
}
