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

internal sealed class DebugWindowStats : DebugWindowCollaborator
{
    internal DebugWindowStats(DebugWindow owner) : base(owner) { }
    internal void DrawGraph(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.15f, 0.8f));

        var series = new[]
        {
            (data: Stats.TokensPerSecondHistory, color: new Color(1f, 1f, 1f, 0.7f),
                label: "RimTalk.DebugWindow.TokensPerSecond".Translate()),
        };

        if (!series.Any(s => s.data != null && s.data.Any())) return;

        long maxVal = Math.Max(1, series.Where(s => s.data != null && s.data.Any()).SelectMany(s => s.data).Max());

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(new Rect(rect.x + 5, rect.y, 40, 20), maxVal.ToString());
        Widgets.Label(new Rect(rect.x + 5, rect.y + rect.height - 15, 60, 20),
            "RimTalk.DebugWindow.SixtySecondsAgo".Translate());
        Widgets.Label(new Rect(rect.xMax - 35, rect.y + rect.height - 15, 40, 20),
            "RimTalk.DebugWindow.Now".Translate());
        GUI.color = Color.white;

        Rect graphArea = rect.ContractedBy(2f);

        foreach (var (data, color, _) in series)
        {
            if (data == null || data.Count < 2) continue;
            const float verticalPadding = 15f;
            float graphHeight = graphArea.height - (2 * verticalPadding);
            if (graphHeight <= 0) continue;

            var points = new List<Vector2>();
            for (int i = 0; i < data.Count; i++)
            {
                float x = graphArea.x + (float)i / (data.Count - 1) * graphArea.width;
                float y = (graphArea.y + graphArea.height - verticalPadding) - ((float)data[i] / maxVal * graphHeight);
                points.Add(new Vector2(x, y));

                if (data[i] > 0 && i > 0 && i % 6 == 0)
                {
                    GUI.color = color;
                    Widgets.Label(new Rect(x - 10, y - 15, 40, 20), data[i].ToString());
                    GUI.color = Color.white;
                }
            }

            for (int i = 0; i < points.Count - 1; i++) Widgets.DrawLine(points[i], points[i + 1], color, 2f);
        }

        var legendRect = new Rect(rect.xMax - 100, rect.y + 10, 90, 30);
        var legendListing = new Listing_Standard();
        Widgets.DrawBoxSolid(legendRect, new Color(0, 0, 0, 0.4f));
        legendListing.Begin(legendRect.ContractedBy(5));
        foreach (var (data, color, label) in series)
        {
            var labelRect = legendListing.GetRect(18);
            Widgets.DrawBoxSolid(new Rect(labelRect.x, labelRect.y + 4, 10, 10), color);
            Widgets.Label(new Rect(labelRect.x + 15, labelRect.y, 70, 20), label);
        }

        legendListing.End();
    }

    internal void DrawStatsSection(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.4f));
        Text.Font = GameFont.Small;
        GUI.BeginGroup(rect);

        const float rowHeight = 22f;
        const float labelWidth = 120f;
        float currentY = 10f;
        var contentRect = rect.AtZero().ContractedBy(10f);

        Color statusColor;
        var aiStatus = Owner._aiStatus.Translate();
        if (aiStatus == "RimTalk.DebugWindow.StatusProcessing".Translate()) statusColor = Color.yellow;
        else if (aiStatus == "RimTalk.DebugWindow.StatusIdle".Translate()) statusColor = Color.green;
        else statusColor = Color.gray;

        GUI.color = Color.gray;
        Widgets.Label(new Rect(contentRect.x, currentY, labelWidth, rowHeight),
            "RimTalk.DebugWindow.AIStatus".Translate());
        GUI.color = statusColor;
        Widgets.Label(new Rect(contentRect.x + labelWidth, currentY, 150f, rowHeight), Owner._aiStatus);
        GUI.color = Color.white;
        currentY += rowHeight;

        void DrawStatRow(string label, string value)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(contentRect.x, currentY, labelWidth, rowHeight), label);
            GUI.color = Color.white;
            Widgets.Label(new Rect(contentRect.x + labelWidth, currentY, 150f, rowHeight), value);
            currentY += rowHeight;
        }

        DrawStatRow("RimTalk.DebugWindow.TotalCalls".Translate(), Owner._totalCalls.ToString("N0"));
        DrawStatRow("RimTalk.DebugWindow.TotalTokens".Translate(), Owner._totalTokens.ToString("N0"));
        DrawStatRow("RimTalk.DebugWindow.AvgCallsPerMin".Translate(), Owner._avgCallsPerMin.ToString("F2"));
        DrawStatRow("RimTalk.DebugWindow.AvgTokensPerMin".Translate(), Owner._avgTokensPerMin.ToString("F2"));
        DrawStatRow("RimTalk.DebugWindow.AvgTokensPerCall".Translate(), Owner._avgTokensPerCall.ToString("F2"));

        GUI.EndGroup();
    }

    internal void DrawBottomActions(Rect rect)
    {
        var listing = new Listing_Standard();
        listing.Begin(rect);

        listing.Gap(6f);

        var settings = Settings.Get();
        bool modEnabled = settings.IsEnabled;
        listing.CheckboxLabeled("RimTalk.DebugWindow.EnableRimTalk".Translate(), ref modEnabled);
        settings.IsEnabled = modEnabled;

        listing.Gap(12f);

        if (listing.ButtonText("RimTalk.DebugWindow.ModSettings".Translate()))
            Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<Settings>()));
        listing.Gap(6f);

        if (listing.ButtonText("RimTalk.DebugWindow.Export".Translate()))
            UIUtil.ExportLogs(Owner._requests);
        listing.Gap(6f);

        var prevColor = GUI.color;
        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (listing.ButtonText("RimTalk.DebugWindow.ResetLogs".Translate()))
            Parts.Details.Reset();
        GUI.color = prevColor;
        listing.End();
    }
}
