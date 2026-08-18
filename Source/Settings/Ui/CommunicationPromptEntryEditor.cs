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

internal sealed class CommunicationPromptEntryEditor : CommunicationSettingsCollaborator
{
    internal CommunicationPromptEntryEditor(Settings owner) : base(owner) { }
    internal void DrawEntryEditor(Rect rect, PromptManager manager, CommunicationSettings settings)
    {
        var p = manager.Presets.FirstOrDefault(p => p.Id == Owner._selectedPresetId);
        if (p == null)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(rect, "RimTalk.Settings.PromptPreset.SelectEntryToEdit".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        float y = rect.y + 2f;

        // --- Layout Constants ---
        float labelX = rect.x + 10f;
        float inputX = rect.x + 130f;
        float inputWidth = 200f;
        float topButtonWidth = 200f;
        float topButtonX = rect.x + rect.width - topButtonWidth - 10f;
        float dropdownWidth = 120f;

        // -- Row 1: Preset Name & Simple Mode --
        Widgets.Label(new Rect(labelX, y, inputX - 10, 24f), "RimTalk.Settings.PromptPreset.PresetName".Translate());
        p.Name = Widgets.TextField(new Rect(inputX, y, inputWidth, 24f), p.Name);

        if (Widgets.ButtonText(new Rect(topButtonX, y, topButtonWidth, 24f),
                "RimTalk.Settings.SwitchToSimpleSettings".Translate()))
        {
            settings.UseAdvancedPromptMode = false;
            Owner._textAreaInitialized = false;
            Owner._aiInstructionPresetId = "";
        }

        y += 28f;

        // -- Row 2: Reset Button --
        if (Widgets.ButtonText(new Rect(topButtonX, y, topButtonWidth, 24f),
                "RimTalk.Settings.ResetToDefault".Translate()))
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RimTalk.Settings.ResetConfirm".Translate(), () =>
            {
                manager.ResetToDefaults();
                Owner._selectedPresetId = null;
                Owner._selectedEntryId = null;
            }));
        }

        var e = p.Entries.FirstOrDefault(x => x.Id == Owner._selectedEntryId);
        if (e == null)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x, y + 28f, rect.width, rect.height - (y + 28f)),
                "RimTalk.Settings.PromptPreset.SelectEntryToEdit".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            return;
        }

        // -- Row 2 (Left Side): Entry Name --
        bool isHistoryMarker = e.IsMainChatHistory;

        Widgets.Label(new Rect(labelX, y, inputX - 10, 24f), "RimTalk.Settings.PromptPreset.EntryName".Translate());
        if (isHistoryMarker)
        {
            GUI.enabled = false;
            Widgets.TextField(new Rect(inputX, y, inputWidth, 24f), CommunicationPromptPresetSettingsPage.LocalizePromptEntryName(e.Name));
            GUI.enabled = true;
        }
        else
        {
            e.Name = Widgets.TextField(new Rect(inputX, y, inputWidth, 24f), e.Name);
        }

        y += 28f;

        // -- Row 3: Role --
        Widgets.Label(new Rect(labelX, y, inputX - 10, 24f), "RimTalk.Settings.PromptPreset.Role".Translate());
        bool hasCustomRole = !string.IsNullOrWhiteSpace(e.CustomRole);
        if (hasCustomRole && e.Role != PromptRole.User) e.Role = PromptRole.User;

        if (isHistoryMarker)
        {
            Widgets.Label(new Rect(inputX, y, dropdownWidth, 24f), e.Role.ToString());
        }
        else if (hasCustomRole)
        {
            Widgets.Label(new Rect(inputX, y, dropdownWidth, 24f), PromptRole.User.ToString());
        }
        else if (Widgets.ButtonText(new Rect(inputX, y, dropdownWidth, 24f), e.Role.ToString()))
        {
            var opts = new List<FloatMenuOption>
            {
                new("System", () => e.Role = PromptRole.System),
                new("User", () => e.Role = PromptRole.User),
                new("Assistant", () => e.Role = PromptRole.Assistant)
            };
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        y += 28f;

        // -- Row 4: Custom Role --
        Widgets.Label(new Rect(labelX, y, inputX - 10, 24f), "RimTalk.Settings.PromptPreset.CustomRole".Translate());
        if (isHistoryMarker)
        {
            Widgets.Label(new Rect(inputX, y, inputWidth, 24f), e.CustomRole ?? "");
        }
        else
        {
            string customRole = Widgets.TextField(new Rect(inputX, y, inputWidth, 24f), e.CustomRole ?? "");
            if (customRole != e.CustomRole) e.CustomRole = customRole;
        }

        y += 28f;

        // -- Row 5: Position --
        float tabRowY = y;
        Widgets.Label(new Rect(labelX, tabRowY, inputX - 10, 24f), "RimTalk.Settings.PromptPreset.Position".Translate());

        if (isHistoryMarker)
        {
            Widgets.Label(new Rect(inputX, tabRowY, dropdownWidth, 24f), e.Position.ToString());
        }
        else if (Widgets.ButtonText(new Rect(inputX, tabRowY, dropdownWidth, 24f), e.Position.ToString()))
        {
            var options = new List<FloatMenuOption>
            {
                new("RimTalk.Settings.PromptPreset.PositionRelative".Translate(),
                    () => e.Position = PromptPosition.Relative),
                new("RimTalk.Settings.PromptPreset.PositionInChat".Translate(),
                    () => e.Position = PromptPosition.InChat)
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (e.Position == PromptPosition.InChat)
        {
            float depthLabelX = inputX + dropdownWidth + 10f;
            Widgets.Label(new Rect(depthLabelX, tabRowY, 50f, 24f), "RimTalk.Settings.PromptPreset.Depth".Translate());
            
            if (Owner._depthBufferEntryId != e.Id)
            {
                Owner._depthBuffer = e.InChatDepth.ToString();
                Owner._depthBufferEntryId = e.Id;
            }

            Owner._depthBuffer = Widgets.TextField(new Rect(depthLabelX + 55f, tabRowY, 60f, 24f), Owner._depthBuffer);
            if (int.TryParse(Owner._depthBuffer, out int res)) e.InChatDepth = res;
        }

        // -- TABS (Toggles) --
        float tabWidth = 70f;
        float rightEdge = rect.xMax - 5f;

        void DrawToggleTab(string label, ref bool isOpen, int indexFromRight, bool isRadio = false, int radioMode = 0)
        {
            Rect tabRect = new Rect(rightEdge - (tabWidth * (indexFromRight + 1)) - (5f * indexFromRight), tabRowY,
                tabWidth, 24f);

            bool active = isRadio
                ? (Owner._showSidePanel && Owner._sidePanelMode == radioMode)
                : isOpen;

            GUI.color = active ? Color.green : Color.white;

            if (Widgets.ButtonText(tabRect, label))
            {
                if (isRadio)
                {
                    if (active) Owner._showSidePanel = false;
                    else
                    {
                        Owner._showSidePanel = true;
                        Owner._sidePanelMode = radioMode;
                        Owner._auxScrollPos = Vector2.zero;
                    }
                }
                else
                {
                    isOpen = !isOpen;
                    if (isOpen) Owner._previewScrollPos = Vector2.zero;
                }
            }

            GUI.color = Color.white;
        }

        // Help (Side Panel Mode 1)
        DrawToggleTab("RimTalk.Settings.PromptHelp".Translate(), ref Owner._showSidePanel, 0, true, 1);

        // Variables (Side Panel Mode 0)
        DrawToggleTab("RimTalk.Settings.ShowVariables".Translate(), ref Owner._showSidePanel, 1, true, 0);

        // Preview (Bottom Panel)
        DrawToggleTab("RimTalk.Settings.PromptPreset.ModePreview".Translate(), ref Owner._showPreview, 2);

        y += 28f;

        // -- MAIN AREA (Layout Split Logic) --
        Rect bottomArea = new Rect(rect.x + 10f, y, rect.width - 20f, rect.yMax - y - 5f);

        // 1. Calculate Horizontal Split (Main vs Side Panel)
        Rect mainWorkRect = bottomArea;
        Rect sidePanelRect = Rect.zero;
        Rect splitHorizRect = Rect.zero;

        float splitterSize = 6f;

        if (Owner._showSidePanel)
        {
            float minMainW = 150f;
            float minSideW = 150f;
            float maxRatioH = (bottomArea.width - minSideW - splitterSize) / bottomArea.width;
            float minRatioH = minMainW / bottomArea.width;

            Owner._splitRatioHoriz = Mathf.Clamp(Owner._splitRatioHoriz, minRatioH, maxRatioH);

            float leftW = (bottomArea.width * Owner._splitRatioHoriz) - (splitterSize / 2f);
            float rightW = bottomArea.width - leftW - splitterSize;

            mainWorkRect = new Rect(bottomArea.x, bottomArea.y, leftW, bottomArea.height);
            splitHorizRect = new Rect(bottomArea.x + leftW, bottomArea.y, splitterSize, bottomArea.height);
            sidePanelRect = new Rect(bottomArea.x + leftW + splitterSize, bottomArea.y, rightW, bottomArea.height);
        }

        // 2. Calculate Vertical Split (Editor vs Preview) within mainWorkRect
        Rect editorRect = mainWorkRect;
        Rect previewRect = Rect.zero;
        Rect splitVertRect = Rect.zero;

        if (Owner._showPreview)
        {
            float minEditorH = 100f;
            float minPrevH = 60f;
            float maxRatioV = (mainWorkRect.height - minPrevH - splitterSize) / mainWorkRect.height;
            float minRatioV = minEditorH / mainWorkRect.height;

            Owner._splitRatioVert = Mathf.Clamp(Owner._splitRatioVert, minRatioV, maxRatioV);

            float topH = (mainWorkRect.height * Owner._splitRatioVert) - (splitterSize / 2f);
            float botH = mainWorkRect.height - topH - splitterSize;

            editorRect = new Rect(mainWorkRect.x, mainWorkRect.y, mainWorkRect.width, topH);
            splitVertRect = new Rect(mainWorkRect.x, mainWorkRect.y + topH, mainWorkRect.width, splitterSize);
            previewRect = new Rect(mainWorkRect.x, mainWorkRect.y + topH + splitterSize, mainWorkRect.width, botH);
        }

        // --- DRAWING ---

        // A. Draw Editor
        float editorInnerWidth = editorRect.width - 20f;
        float editorContentHeight = Mathf.Ceil(Mathf.Max(editorRect.height, Text.CalcHeight(e.Content, editorInnerWidth) + 25f));
        Rect editorViewRect = new Rect(0f, 0f, editorInnerWidth, editorContentHeight);

        const string editorControlName = "PromptEntryEditor";
        Widgets.BeginScrollView(editorRect, ref Owner._promptContentScrollPos, editorViewRect);
        GUI.SetNextControlName(editorControlName);
        
        string newContent = Widgets.TextArea(new Rect(0f, 0f, editorInnerWidth, editorContentHeight), e.Content);
        
        // Auto-scroll logic: only scroll if the cursor position changed
        if (GUI.GetNameOfFocusedControl() == editorControlName)
        {
            TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te != null && te.cursorIndex != Owner._lastPromptEditorCursorPos)
            {
                Owner._lastPromptEditorCursorPos = te.cursorIndex;
                float cursorY = te.graphicalCursorPos.y;
                if (cursorY < Owner._promptContentScrollPos.y)
                    Owner._promptContentScrollPos.y = cursorY;
                else if (cursorY + 25f > Owner._promptContentScrollPos.y + editorRect.height)
                    Owner._promptContentScrollPos.y = cursorY + 25f - editorRect.height;
            }
        }
        Widgets.EndScrollView();

        if (newContent != e.Content)
        {
            e.Content = newContent;
            if (Owner._showSidePanel && Owner._sidePanelMode == 0) Pages.PromptPreset.UpdateSmartFilter(newContent);
        }

        // B. Draw Preview (if active)
        if (Owner._showPreview)
        {
            Widgets.DrawBoxSolid(splitVertRect, new Color(0.2f, 0.2f, 0.2f));
            GUI.DrawTexture(new Rect(splitVertRect.center.x - 10f, splitVertRect.center.y - 2f, 20f, 4f),
                BaseContent.WhiteTex);
            Widgets.DrawHighlightIfMouseover(splitVertRect);

            // Handle Vertical Splitter Input
            if (Event.current.type == EventType.MouseDown && splitVertRect.Contains(Event.current.mousePosition))
            {
                Owner._isDraggingVert = true;
                Event.current.Use();
            }

            Widgets.DrawBoxSolid(previewRect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            Pages.PromptPreset.DrawPreviewContent(previewRect, e.Content);
        }

        // C. Draw Side Panel (if active)
        if (Owner._showSidePanel)
        {
            Widgets.DrawBoxSolid(splitHorizRect, new Color(0.2f, 0.2f, 0.2f));
            GUI.DrawTexture(new Rect(splitHorizRect.center.x - 2f, splitHorizRect.center.y - 10f, 4f, 20f),
                BaseContent.WhiteTex);
            Widgets.DrawHighlightIfMouseover(splitHorizRect);

            // Handle Horizontal Splitter Input
            if (Event.current.type == EventType.MouseDown && splitHorizRect.Contains(Event.current.mousePosition))
            {
                Owner._isDraggingHoriz = true;
                Event.current.Use();
            }

            Pages.PromptSide.DrawSidePanel(sidePanelRect, manager, e);
        }

        // D. Handle Drag Logic (Global)
        if (Owner._isDraggingVert)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                Owner._splitRatioVert += Event.current.delta.y / mainWorkRect.height;
                Event.current.Use();
            }

            if (Event.current.rawType == EventType.MouseUp) Owner._isDraggingVert = false;
        }

        if (Owner._isDraggingHoriz)
        {
            if (Event.current.type == EventType.MouseDrag)
            {
                Owner._splitRatioHoriz += Event.current.delta.x / bottomArea.width;
                Event.current.Use();
            }

            if (Event.current.rawType == EventType.MouseUp) Owner._isDraggingHoriz = false;
        }
    }
}
