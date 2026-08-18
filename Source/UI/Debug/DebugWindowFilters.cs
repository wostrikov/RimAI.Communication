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

internal sealed class DebugWindowFilters : DebugWindowCollaborator
{
    internal DebugWindowFilters(DebugWindow owner) : base(owner) { }
    internal void DrawLeftPane(Rect rect)
    {
        // Filter Bar (Integrated at top)
        var filterRect = new Rect(rect.x, rect.y, rect.width, DebugWindow.FilterBarHeight);
        DrawInternalFilterBar(filterRect);

        // Table Area
        var tableRect = new Rect(rect.x, rect.y + DebugWindow.FilterBarHeight, rect.width, rect.height - DebugWindow.FilterBarHeight);

        switch (Owner._viewMode)
        {
            case DebugViewMode.ActiveRequests:
                Parts.ActiveRequests.DrawActiveRequestsTable(tableRect);
                break;
            case DebugViewMode.GroupedByPawn:
                Parts.Grouped.DrawGroupedPawnTable(tableRect);
                break;
            case DebugViewMode.MainTable:
            default:
                Parts.Console.DrawConsoleTable(tableRect);
                break;
        }
    }

    internal void DrawInternalFilterBar(Rect rect)
    {
        float y = rect.y + 3f;
        float height = 24f;
        float gap = 5f;
        float startX = rect.x;
        float viewDropdownWidth = 90f;
        float statusWidth = 100f;
        float limitWidth = 90f;
        float totalFixedSpace = viewDropdownWidth + statusWidth + limitWidth + (4 * gap);
        float flexSpace = rect.width - totalFixedSpace;
        if (flexSpace < 50f) flexSpace = 50f;
        float pawnFilterWidth = flexSpace * 0.35f;
        float textSearchWidth = flexSpace * 0.65f;

        float currentX = startX;

        // 1. View Mode Dropdown
        var viewBtnRect = new Rect(currentX, y, viewDropdownWidth, height);
        string viewLabel = Owner._viewMode switch
        {
            DebugViewMode.MainTable => "RimTalk.DebugWindow.ViewByTime".Translate(),
            DebugViewMode.GroupedByPawn => "RimTalk.DebugWindow.ViewByPawn".Translate(),
            DebugViewMode.ActiveRequests => "RimTalk.DebugWindow.ViewTalkRequests".Translate(),
            _ => Owner._viewMode.ToString()
        };

        if (Widgets.ButtonText(viewBtnRect, viewLabel))
        {
            var options = new List<FloatMenuOption>
            {
                new("RimTalk.DebugWindow.ViewByTime".Translate(), () => Owner._viewMode = DebugViewMode.MainTable),
                new("RimTalk.DebugWindow.ViewByPawn".Translate(), () => Owner._viewMode = DebugViewMode.GroupedByPawn),
                new("RimTalk.DebugWindow.ViewTalkRequests".Translate(), () => Owner._viewMode = DebugViewMode.ActiveRequests)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        currentX += viewDropdownWidth + gap;

        // 2. Pawn Filter
        Owner._pawnFilter = DrawSearchField(new Rect(currentX, y, pawnFilterWidth, height), Owner._pawnFilter,
            "RimTalk.DebugWindow.FilterPawn".Translate(), DebugWindow.ControlNamePawnFilter);
        currentX += pawnFilterWidth + gap;

        // 3. Text Search
        Owner._textSearch = DrawSearchField(new Rect(currentX, y, textSearchWidth, height), Owner._textSearch,
            "RimTalk.DebugWindow.Search".Translate(), DebugWindow.ControlNameTextSearch);
        currentX += textSearchWidth + gap;

        // 4. Status Dropdown
        var stateBtnRect = new Rect(currentX, y, statusWidth, height);
        if (Owner._viewMode == DebugViewMode.ActiveRequests)
        {
            string label = Owner._activeRequestStatusFilter.HasValue
                ? $"RimTalk.DebugWindow.State{Owner._activeRequestStatusFilter.Value}".Translate()
                : "RimTalk.DebugWindow.StateAll".Translate();

            if (Widgets.ButtonText(stateBtnRect, label))
            {
                var options = new List<FloatMenuOption>
                {
                    new("RimTalk.DebugWindow.StateAll".Translate(), () => Owner._activeRequestStatusFilter = null)
                };
                options.AddRange(Enum.GetValues(typeof(RequestStatus))
                    .Cast<RequestStatus>()
                    .Select(s => new FloatMenuOption($"RimTalk.DebugWindow.State{s}".Translate(),
                        () => Owner._activeRequestStatusFilter = s)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        else
        {
            if (Widgets.ButtonText(stateBtnRect, Owner._stateFilter.GetLabel()))
            {
                var options = Enum.GetValues(typeof(State))
                    .Cast<State>()
                    .Select(filter => new FloatMenuOption(filter.GetLabel(), () => Owner._stateFilter = filter))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        currentX += statusWidth + gap;

        // 5. Limit Dropdown
        var limitBtnRect = new Rect(currentX, y, limitWidth, height);
        string lastPrefix = "RimTalk.DebugWindow.Last".Translate();

        if (Widgets.ButtonText(limitBtnRect, $"{lastPrefix} {Owner._maxRows}"))
        {
            var options = new List<FloatMenuOption>
            {
                new($"{lastPrefix} 200", () => Owner._maxRows = 200),
                new($"{lastPrefix} 500", () => Owner._maxRows = 500),
                new($"{lastPrefix} 1000", () => Owner._maxRows = 1000),
                new($"{lastPrefix} 2000", () => Owner._maxRows = 2000)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
    internal IEnumerable<ApiLog> ApplyFilters(IEnumerable<ApiLog> source)
    {
        IEnumerable<ApiLog> q = source;

        if (!string.IsNullOrWhiteSpace(Owner._pawnFilter))
        {
            var needle = Owner._pawnFilter.Trim();
            q = q.Where(r => (r.Name ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(Owner._textSearch))
        {
            var needle = Owner._textSearch.Trim();
            q = q.Where(r =>
                (r.TalkRequest.Prompt ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.Response ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.InteractionType ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (Owner._stateFilter != State.None)
            q = q.Where(r => r.GetState() == Owner._stateFilter);

        return q;
    }

    internal void HandleGlobalClicks(Rect inRect)
    {
        if (Event.current.type == EventType.MouseDown && inRect.Contains(Event.current.mousePosition))
        {
            string focused = GUI.GetNameOfFocusedControl();
            if (focused == DebugWindow.ControlNamePawnFilter ||
                focused == DebugWindow.ControlNameTextSearch ||
                focused == DebugWindow.ControlNameDetailResponse ||
                focused == DebugWindow.ControlNameDetailMessages)
            {
                GUI.FocusControl(null);
            }
        }
    }

    internal string DrawSearchField(Rect rect, string text, string placeholder, string controlName)
    {
        GUI.SetNextControlName(controlName);
        string result = Widgets.TextField(rect, text);

        if (string.IsNullOrEmpty(result))
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            Text.Anchor = TextAnchor.MiddleLeft;
            var labelRect = new Rect(rect.x + 5f, rect.y, rect.width - 5f, rect.height);
            Widgets.Label(labelRect, placeholder);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
        }

        return result;
    }
}
