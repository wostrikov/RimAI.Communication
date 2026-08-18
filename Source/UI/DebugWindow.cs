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

public class DebugWindow : Window
{
    internal DebugWindowParts Parts;

    // Layout Constants
    internal const float RowHeight = 22f;
    internal const float FilterBarHeight = 30f;
    internal const float HeaderHeight = 22f;
    internal const float ColumnPadding = 10f;

    // Column Widths
    internal const float TimestampColumnWidth = 65f;
    internal const float PawnColumnWidth = 80f;
    internal const float TimeColumnWidth = 55f;
    internal const float TokensColumnWidth = 50f;
    internal const float StateColumnWidth = 65f;
    internal const float InteractionTypeColumnWidth = 50f;

    // Active Request Column Widths
    internal const float ARTimeWidth = 65f;
    internal const float ARInitiatorWidth = 80f;
    internal const float ARRecipientWidth = 80f;
    internal const float ARTypeWidth = 60f;
    internal const float ARElapsedWidth = 60f;
    internal const float ARStatusWidth = 60f;

    // Grouping Column Widths
    internal const float GroupedPawnNameWidth = 80f;
    internal const float GroupedRequestsWidth = 60f;
    internal const float GroupedLastTalkWidth = 60f;
    internal const float GroupedChattinessWidth = 65f;
    internal const float GroupedExpandIconWidth = 25f;
    internal const float GroupedStatusWidth = 60f;

    internal readonly string _generating = "RimTalk.DebugWindow.Generating".Translate();

    // State Variables
    internal Vector2 _tableScrollPosition;
    internal Vector2 _activeRequestsScrollPosition;
    internal Vector2 _detailsScrollPosition;
    internal bool _stickToBottom = true;

    internal string _aiStatus;

    // Stats
    internal long _totalCalls;
    internal long _totalTokens;
    internal double _avgCallsPerMin;
    internal double _avgTokensPerMin;
    internal double _avgTokensPerCall;

    internal List<PawnState> _pawnStates;
    internal List<ApiLog> _requests;
    internal List<TalkRequest> _cachedActiveViewList = [];
    internal readonly Dictionary<string, List<ApiLog>> _talkLogsByPawn = new();

    // Controls
    internal int _maxRows;
    internal string _pawnFilter;
    internal string _textSearch;
    internal State _stateFilter;
    internal RequestStatus? _activeRequestStatusFilter;
    internal ApiLog _selectedLog;

    // Temporary Editable State
    internal Guid _selectedRequestIdForTemp = Guid.Empty;
    internal string _tempResponse;
    internal string _tempPromptSegmentsText;
    internal List<PromptMessageSegment> _tempPromptSegments = [];
    internal readonly HashSet<int> _expandedPromptSegmentIndices = new();

    internal DebugViewMode _viewMode;
    internal string _sortColumn;
    internal bool _sortAscending;
    internal readonly List<string> _expandedPawns;

    // Focus Control Names
    internal const string ControlNamePawnFilter = "PawnFilterField";
    internal const string ControlNameTextSearch = "TextSearchField";
    internal const string ControlNameDetailResponse = "DetailResponseField";
    internal const string ControlNameDetailMessages = "DetailMessagesField";

    // Styles
    internal GUIStyle _contextStyle;
    internal GUIStyle _monoTinyStyle;

    public DebugWindow()
    {
        doCloseX = true;
        draggable = true;
        resizeable = true;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        closeOnAccept = false;
        closeOnCancel = true;
        preventCameraMotion = false;

        var settings = Settings.Get();
        _viewMode = 0;
        _sortColumn = settings.DebugSortColumn;
        _sortAscending = settings.DebugSortAscending;
        _expandedPawns = [];

        _maxRows = 500;
        _pawnFilter = string.Empty;
        _textSearch = string.Empty;
        _stateFilter = State.None;
        _activeRequestStatusFilter = null;
        Parts = new DebugWindowParts(this);
    }

    public override Vector2 InitialSize => new(1100f, 600f);

    public override void PreClose()
    {
        base.PreClose();
        var settings = Settings.Get();
        settings.DebugSortColumn = _sortColumn;
        settings.DebugSortAscending = _sortAscending;
        settings.Write();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Parts.Filters.HandleGlobalClicks(inRect);
        UpdateData();

        const float bottomSectionHeight = 150f;
        const float spacing = 10f;

        float contentHeight = inRect.height - bottomSectionHeight - spacing;

        // LEFT PANE (Table + Filters) vs RIGHT PANE (Details)
        float leftWidth = inRect.width * 0.60f - (spacing / 2);
        float rightWidth = inRect.width * 0.40f - (spacing / 2);

        var leftPaneRect = new Rect(inRect.x, inRect.y, leftWidth, contentHeight);
        var detailsRect = new Rect(leftPaneRect.xMax + spacing, inRect.y, rightWidth, contentHeight);

        Parts.Filters.DrawLeftPane(leftPaneRect);
        Parts.Details.DrawDetailsPanel(detailsRect);

        // Bottom Section
        var bottomRect = new Rect(inRect.x, leftPaneRect.yMax + spacing, inRect.width, bottomSectionHeight);
        float graphWidth = bottomRect.width * 0.50f;
        float statsWidth = bottomRect.width * 0.30f;
        float actionsWidth = bottomRect.width * 0.20f - (spacing * 2);

        var graphRect = new Rect(bottomRect.x, bottomRect.y, graphWidth, bottomRect.height);
        var statsRect = new Rect(graphRect.xMax + spacing, bottomRect.y, statsWidth, bottomRect.height);
        var actionsRect = new Rect(statsRect.xMax + spacing, bottomRect.y, actionsWidth, bottomRect.height);

        Parts.Stats.DrawGraph(graphRect);
        Parts.Stats.DrawStatsSection(statsRect);
        Parts.Stats.DrawBottomActions(actionsRect);
    }

    internal void UpdateData()
    {
        var settings = Settings.Get();
        if (!settings.IsEnabled)
            _aiStatus = "RimTalk.DebugWindow.StatusDisabled".Translate();
        else
            _aiStatus = AIService.IsBusy()
                ? "RimTalk.DebugWindow.StatusProcessing".Translate()
                : "RimTalk.DebugWindow.StatusIdle".Translate();

        _totalCalls = Stats.TotalCalls;
        _totalTokens = Stats.TotalTokens;
        _avgCallsPerMin = Stats.AvgCallsPerMinute;
        _avgTokensPerMin = Stats.AvgTokensPerMinute;
        _avgTokensPerCall = Stats.AvgTokensPerCall;
        _pawnStates = Cache.GetAll().ToList();

        var allHistory = ApiHistory.GetAll();
        var filtered = Parts.Filters.ApplyFilters(allHistory);
        var apiLogs = filtered as ApiLog[] ?? filtered.ToArray();
        int count = apiLogs.Count();
        _requests = count > _maxRows ? apiLogs.Skip(count - _maxRows).ToList() : apiLogs.ToList();

        _talkLogsByPawn.Clear();
        foreach (var request in _requests.Where(r => r.Name != null))
        {
            if (!_talkLogsByPawn.ContainsKey(request.Name))
                _talkLogsByPawn[request.Name] = [];
            _talkLogsByPawn[request.Name].Add(request);
        }

        // If selected request is no longer in the filtered list, deselect
        if (_selectedLog != null && _requests.All(r => r.Id != _selectedLog.Id))
        {
            _selectedLog = null;
            _selectedRequestIdForTemp = Guid.Empty;
        }

        var tempActiveList = new List<TalkRequest>();
        tempActiveList.AddRange(TalkRequestPool.GetAllActive());
        tempActiveList.AddRange(TalkRequestPool.GetHistory());
        if (_pawnStates != null)
            tempActiveList.AddRange(_pawnStates.SelectMany(state => state.TalkRequests));

        IEnumerable<TalkRequest> q = tempActiveList;

        if (!string.IsNullOrWhiteSpace(_pawnFilter))
        {
            var needle = _pawnFilter.Trim();
            q = q.Where(r =>
                (r.Initiator?.LabelShort ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (r.Recipient?.LabelShort ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
            );
        }

        if (!string.IsNullOrWhiteSpace(_textSearch))
        {
            var needle = _textSearch.Trim();
            q = q.Where(r => (r.Prompt ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (_activeRequestStatusFilter.HasValue)
        {
            q = q.Where(r => r.Status == _activeRequestStatusFilter.Value);
        }

        _cachedActiveViewList = q.ToList();
        _cachedActiveViewList.Sort((a, b) => a.CreatedTime.CompareTo(b.CreatedTime));
    }
}
