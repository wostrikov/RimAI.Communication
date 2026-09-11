using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Communication.UI;

/// <summary>
/// Which vanilla and mod interactions are voiced at once instead of waiting their turn,
/// grouped by the mod that adds them.
/// </summary>
public class Dialog_FastTrackInteractions : Window
{
    private Vector2 _scrollPosition = Vector2.zero;
    private string _filterText = "";
    private readonly HashSet<string> _collapsedMods = new();
    private List<IGrouping<string, InteractionDef>> _cachedGroups;
    private string _lastFilter;

    public Dialog_FastTrackInteractions()
    {
        doCloseX = true;
        closeOnAccept = true;
        closeOnCancel = true;
        draggable = true;
        absorbInputAroundWindow = true;
        preventCameraMotion = false;
    }

    public override Vector2 InitialSize => new(650f, 620f);

    private static IEnumerable<InteractionDef> GetEligibleInteractionDefs()
    {
        return DefDatabase<InteractionDef>.AllDefsListForReading
            .Where(def => def.defName != "RimTalkInteraction" && !def.defName.StartsWith("RimTalk"));
    }

    private List<IGrouping<string, InteractionDef>> GetInteractionGroups()
    {
        if (_cachedGroups != null && _filterText == _lastFilter)
            return _cachedGroups;

        _lastFilter = _filterText;
        IEnumerable<InteractionDef> filtered = GetEligibleInteractionDefs();
        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            string search = _filterText.Trim();
            filtered = filtered.Where(def =>
                (def.label != null && def.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (def.defName != null && def.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (def.modContentPack?.Name != null && def.modContentPack.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        _cachedGroups = filtered
            .OrderBy(def => def.label ?? def.defName)
            .GroupBy(def => def.modContentPack?.Name ?? "Core")
            .OrderBy(g => g.Key.Equals("Core", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(g => g.Key)
            .ToList();

        return _cachedGroups;
    }

    private static void SetAll(IEnumerable<InteractionDef> defs, bool value)
    {
        var settings = Settings.Get();
        foreach (var def in defs)
            settings.FastTrackInteractions[def.defName] = value;
        settings.Write();
    }

    public override void DoWindowContents(Rect inRect)
    {
        var settings = Settings.Get();

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "RimTalk.Settings.FastTrackInteractionsTitle".Translate());

        Text.Font = GameFont.Tiny;
        GUI.color = new Color(0.85f, 0.85f, 0.85f);
        string desc = "RimTalk.Settings.FastTrackInteractionsDesc".Translate();
        float descHeight = Text.CalcHeight(desc, inRect.width);
        Widgets.Label(new Rect(0f, 34f, inRect.width, descHeight), desc);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        float controlY = 38f + descHeight;
        Rect searchRect = new Rect(0f, controlY, inRect.width - 230f, 28f);
        _filterText = Widgets.TextField(searchRect, _filterText);

        Rect selectAllRect = new Rect(searchRect.xMax + 10f, controlY, 100f, 28f);
        if (Widgets.ButtonText(selectAllRect, "RimTalk.Settings.SelectAll".Translate()))
            SetAll(GetEligibleInteractionDefs(), true);

        Rect deselectAllRect = new Rect(selectAllRect.xMax + 10f, controlY, 100f, 28f);
        if (Widgets.ButtonText(deselectAllRect, "RimTalk.Settings.DeselectAll".Translate()))
            SetAll(GetEligibleInteractionDefs(), false);

        float listY = controlY + 36f;
        float listHeight = inRect.height - listY - 45f;
        Rect outRect = new Rect(0f, listY, inRect.width, listHeight);

        var groups = GetInteractionGroups();
        float viewHeight = 20f;
        foreach (var group in groups)
            viewHeight += 28f + (_collapsedMods.Contains(group.Key) ? 0f : group.Count() * 26f);

        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(viewHeight, listHeight));
        Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);

        float curY = 0f;
        foreach (var group in groups)
        {
            bool isCollapsed = _collapsedMods.Contains(group.Key);
            Rect headerRect = new Rect(0f, curY, viewRect.width, 24f);
            Widgets.DrawHighlightIfMouseover(headerRect);

            Rect toggleRect = new Rect(headerRect.x, headerRect.y, 20f, 24f);
            if (Widgets.ButtonText(toggleRect, isCollapsed ? "[+]" : "[-]", drawBackground: false))
            {
                if (isCollapsed) _collapsedMods.Remove(group.Key);
                else _collapsedMods.Add(group.Key);
            }

            int enabledCount = group.Count(def => settings.IsFastTrackInteraction(def.defName));
            MultiCheckboxState state = enabledCount == 0 ? MultiCheckboxState.Off
                : enabledCount == group.Count() ? MultiCheckboxState.On
                : MultiCheckboxState.Partial;

            Rect checkRect = new Rect(viewRect.width - 24f, headerRect.y, 24f, 24f);
            if (Widgets.CheckboxMulti(checkRect, state) != state)
                SetAll(group, state != MultiCheckboxState.On);

            Rect labelRect = new Rect(toggleRect.xMax + 6f, headerRect.y, checkRect.x - toggleRect.xMax - 12f, 24f);
            GUI.color = Color.cyan;
            Widgets.Label(labelRect, $"{group.Key} ({group.Count()})");
            GUI.color = Color.white;

            if (Widgets.ButtonInvisible(labelRect))
            {
                bool target = state != MultiCheckboxState.On;
                SetAll(group, target);
                (target ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
            }

            curY += 26f;
            if (isCollapsed) continue;

            foreach (var def in group)
            {
                Rect rowRect = new Rect(24f, curY, viewRect.width - 24f, 24f);
                Widgets.DrawHighlightIfMouseover(rowRect);

                bool isEnabled = settings.IsFastTrackInteraction(def.defName);
                bool newValue = isEnabled;
                string label = !string.IsNullOrWhiteSpace(def.label) ? $"{def.LabelCap} ({def.defName})" : def.defName;
                Widgets.CheckboxLabeled(rowRect, label, ref newValue);
                if (newValue != isEnabled)
                {
                    settings.FastTrackInteractions[def.defName] = newValue;
                    settings.Write();
                }

                if (!string.IsNullOrWhiteSpace(def.description))
                    TooltipHandler.TipRegion(rowRect, def.description);

                curY += 26f;
            }
        }

        Widgets.EndScrollView();

        if (Widgets.ButtonText(new Rect((inRect.width - 120f) / 2f, inRect.height - 35f, 120f, 30f), "Close".Translate()))
            Close();
    }
}
