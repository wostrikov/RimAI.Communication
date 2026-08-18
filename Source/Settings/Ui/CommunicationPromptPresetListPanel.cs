using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ustas.RimAI.Communication.Prompt;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Communication;

internal sealed class CommunicationPromptPresetListPanel : CommunicationSettingsCollaborator
{
    internal CommunicationPromptPresetListPanel(Settings owner) : base(owner) { }
    internal void DrawPresetListPanel(Rect rect, PromptManager manager)
    {
        Widgets.DrawBoxSolid(rect, CommunicationPromptPresetSettingsPage.LeftPanelBackground);

        float buttonSize = 20f;
        float listPaddingX = 2f;
        float scrollBarWidth = 16f;
        float listWidth = rect.width - (listPaddingX * 2);
        float viewWidth = listWidth - scrollBarWidth;
        float rowButtonX = viewWidth - buttonSize - 2f;
        float headerButtonX = listPaddingX + rowButtonX;
        float y = rect.y + 5f;

        // Presets Header
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(new Rect(rect.x + 5f, y, rect.width - 35f, 20f),
            "RimTalk.Settings.PromptPreset.Presets".Translate());

        GUI.color = CommunicationPromptPresetSettingsPage.AddGreen;
        Rect addPresetRect = new Rect(rect.x + headerButtonX, y, buttonSize, buttonSize);
        if (Widgets.ButtonText(addPresetRect, "+"))
        {
            // Use CreateNewPreset to generate from factory default instead of duplicating current
            var p = manager.CreateNewPreset("RimTalk.Settings.PromptPreset.NewPresetName".Translate());
            if (p != null)
            {
                Owner._selectedPresetId = p.Id;
                Owner._selectedEntryId = p.Entries.FirstOrDefault()?.Id;
            }
        }

        GUI.color = Color.white;
        TooltipHandler.TipRegion(addPresetRect, "RimTalk.Settings.PromptPreset.NewPreset".Translate());
        y += 22f;

        // Preset ScrollView
        Text.Font = GameFont.Small;
        Rect listRect = new Rect(rect.x + listPaddingX, y, listWidth, 150f);
        Rect viewRect = new Rect(0f, 0f, viewWidth, manager.Presets.Count * 25f);
        Widgets.BeginScrollView(listRect, ref Owner._presetListScrollPos, viewRect);
        float py = 0f;
        for (int i = 0; i < manager.Presets.Count; i++)
        {
            var p = manager.Presets[i];
            Rect row = new Rect(0f, py, viewRect.width, 24f);
            if (Owner._selectedPresetId == p.Id) Widgets.DrawHighlight(row);

            if (p.IsActive)
            {
                GUI.color = Color.green;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(4f, py, 16f, 24f), "▶");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            if (Widgets.ButtonText(new Rect(24f, py, viewRect.width - 48f, 24f), p.Name, false))
            {
                Owner._selectedPresetId = p.Id;
                Owner._selectedEntryId = p.Entries.FirstOrDefault()?.Id;
            }

            if (p.Name != CommunicationPromptPresetSettingsPage.DefaultPresetName && manager.Presets.Count > 1)
            {
                Rect delRect = new Rect(rowButtonX, py + 2f, buttonSize, buttonSize);
                GUI.color = CommunicationPromptPresetSettingsPage.DeleteRed;
                if (Widgets.ButtonText(delRect, "×"))
                {
                    manager.RemovePreset(p.Id);
                    if (Owner._selectedPresetId == p.Id)
                    {
                        var next = manager.Presets.FirstOrDefault();
                        Owner._selectedPresetId = next?.Id;
                        Owner._selectedEntryId = next?.Entries.FirstOrDefault()?.Id;
                    }
                }

                GUI.color = Color.white;
                TooltipHandler.TipRegion(delRect, "RimTalk.Settings.PromptPreset.Delete".Translate());
            }

            py += 25f;
        }

        Widgets.EndScrollView();
        y += 155f;

        var sel = manager.Presets.FirstOrDefault(x => x.Id == Owner._selectedPresetId);
        if (sel != null)
        {
            float btnW2 = (rect.width - 15f) / 2f;

            bool isAlreadyActive = sel.IsActive;
            if (isAlreadyActive) GUI.enabled = false;
            if (Widgets.ButtonText(new Rect(rect.x + 5f, y, btnW2, 24f),
                    "RimTalk.Settings.PromptPreset.Activate".Translate()))
            {
                manager.SetActivePreset(sel.Id);
            }

            if (isAlreadyActive) GUI.enabled = true;

            if (Widgets.ButtonText(new Rect(rect.x + 10f + btnW2, y, btnW2, 24f),
                    "RimTalk.Settings.PromptPreset.Duplicate".Translate()))
            {
                var c = manager.DuplicatePreset(sel.Id);
                if (c != null)
                {
                    Owner._selectedPresetId = c.Id;
                    Owner._selectedEntryId = c.Entries.FirstOrDefault()?.Id;
                }
            }

            y += 28f;

            if (Widgets.ButtonText(new Rect(rect.x + 5f, y, btnW2, 24f),
                    "RimTalk.Settings.PromptPreset.Import".Translate())) Pages.PromptSide.ShowImportMenu(manager);
            if (Widgets.ButtonText(new Rect(rect.x + 10f + btnW2, y, btnW2, 24f),
                    "RimTalk.Settings.PromptPreset.Export".Translate()))
            {
                if (PresetSerializer.ExportToFile(sel))
                {
                    var exportDir = PresetSerializer.GetExportDirectory();
                    Messages.Message("RimTalk.Settings.PromptPreset.ExportSuccess".Translate(exportDir),
                        MessageTypeDefOf.PositiveEvent, false);
                }
                else
                    Messages.Message("RimTalk.Settings.PromptPreset.ExportFailed".Translate(),
                        MessageTypeDefOf.RejectInput, false);
            }

            y += 32f;
        }

        y += 5f;

        if (sel != null)
        {
            float ey = 0f;
            const float entryHeaderHeight = 22f;
            GUI.color = CommunicationPromptPresetSettingsPage.AddGreen;
            Rect addRect = new Rect(rect.x + headerButtonX, y, buttonSize, buttonSize);
            if (Widgets.ButtonText(addRect, "+"))
            {
                var newEntry = new PromptEntry("RimTalk.Settings.PromptPreset.NewEntryName".Translate(), "",
                    PromptRole.User)
                {
                    Position = PromptPosition.Relative
                };

                int insertIndex = sel.Entries.Count;
                if (!string.IsNullOrEmpty(Owner._selectedEntryId))
                {
                    int currentIndex = sel.Entries.FindIndex(en => en.Id == Owner._selectedEntryId);
                    if (currentIndex >= 0) insertIndex = currentIndex + 1;
                }

                sel.Entries.Insert(insertIndex, newEntry);
                Owner._selectedEntryId = newEntry.Id;
            }

            GUI.color = Color.white;
            TooltipHandler.TipRegion(addRect, "RimTalk.Settings.PromptPreset.NewEntry".Translate());

            Text.Font = GameFont.Small;
            float listTop = y + entryHeaderHeight;
            Rect eListRect = new Rect(rect.x + listPaddingX, listTop, listWidth, rect.yMax - listTop - 35f);
            Rect eViewRect = new Rect(0f, 0f, viewWidth, sel.Entries.Count * 25f);
            Widgets.BeginScrollView(eListRect, ref Owner._entryListScrollPos, eViewRect);

            for (int i = 0; i < sel.Entries.Count; i++)
            {
                var entry = sel.Entries[i];
                Rect erow = new Rect(0f, ey, eViewRect.width, 24f);
                if (Owner._selectedEntryId == entry.Id) Widgets.DrawHighlight(erow);

                bool isHistoryMarker = entry.IsMainChatHistory;

                bool en = entry.Enabled;
                Widgets.Checkbox(new Vector2(4f, ey + 4f), ref en, 16f);
                entry.Enabled = en;

                if (Widgets.ButtonText(new Rect(24f, ey, eViewRect.width - 48f, 24f), CommunicationPromptPresetSettingsPage.LocalizePromptEntryName(entry.Name), false))
                    Owner._selectedEntryId = entry.Id;

                if (!isHistoryMarker)
                {
                    Rect edel = new Rect(rowButtonX, ey + 2f, buttonSize, buttonSize);
                    GUI.color = CommunicationPromptPresetSettingsPage.DeleteRed;
                    if (Widgets.ButtonText(edel, "×"))
                    {
                        sel.RemoveEntry(entry.Id);
                        if (Owner._selectedEntryId == entry.Id) Owner._selectedEntryId = sel.Entries.FirstOrDefault()?.Id;
                    }

                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(edel, "RimTalk.Settings.PromptPreset.Delete".Translate());
                }

                ey += 25f;
            }

            Widgets.EndScrollView();

            if (Owner._selectedEntryId != null)
            {
                var selectedEntry = sel.GetEntry(Owner._selectedEntryId);
                if (selectedEntry != null)
                {
                    int index = sel.Entries.IndexOf(selectedEntry);

                    float sw = (rect.width - 15f) / 2f;
                    
                    // Up button
                    if (index > 0)
                    {
                        if (Widgets.ButtonText(new Rect(rect.x + 5f, rect.yMax - 32f, sw, 24f), "▲"))
                        {
                            sel.Entries.RemoveAt(index);
                            sel.Entries.Insert(index - 1, selectedEntry);
                        }
                    }
                    else
                    {
                        GUI.enabled = false;
                        Widgets.ButtonText(new Rect(rect.x + 5f, rect.yMax - 32f, sw, 24f), "▲");
                        GUI.enabled = true;
                    }

                    // Down button
                    if (index < sel.Entries.Count - 1)
                    {
                        if (Widgets.ButtonText(new Rect(rect.x + 10f + sw, rect.yMax - 32f, sw, 24f), "▼"))
                        {
                            sel.Entries.RemoveAt(index);
                            sel.Entries.Insert(index + 1, selectedEntry);
                        }
                    }
                    else
                    {
                        GUI.enabled = false;
                        Widgets.ButtonText(new Rect(rect.x + 10f + sw, rect.yMax - 32f, sw, 24f), "▼");
                        GUI.enabled = true;
                    }
                }
            }
        }
    }
}
