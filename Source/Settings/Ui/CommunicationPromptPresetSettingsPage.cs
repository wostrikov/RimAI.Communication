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

internal sealed class CommunicationPromptPresetSettingsPage : CommunicationSettingsCollaborator
{
    internal CommunicationPromptPresetSettingsPage(Settings owner) : base(owner) { }
    internal static readonly Color LeftPanelBackground = new(0.05f, 0.05f, 0.05f, 0.55f);
    internal static readonly Color AddGreen = new(0.3f, 0.9f, 0.3f);
    internal static readonly Color DeleteRed = new(1f, 0.4f, 0.4f);
    internal const string DefaultPresetName = "RimTalk Default";

    internal void DrawPromptPresetSettings(Listing_Standard listingStandard, Rect inRect)
    {
        CommunicationSettings settings = Settings.Get();
        if (settings.UseAdvancedPromptMode)
            DrawAdvancedPromptMode(listingStandard, settings, inRect);
        else
            DrawSimplePromptMode(listingStandard, settings);
    }

    internal void DrawSimplePromptMode(Listing_Standard listingStandard, CommunicationSettings settings)
    {
        Pages.AiInstruction.DrawAIInstructionSettings(listingStandard, showAdvancedSwitch: true);
    }

    internal void DrawAdvancedPromptMode(Listing_Standard listingStandard, CommunicationSettings settings, Rect containerRect)
    {
        var manager = PromptManager.Instance;
        if (string.IsNullOrEmpty(Owner._selectedPresetId))
        {
            var active = manager.Presets.FirstOrDefault(p => p.IsActive) ??
                         manager.Presets.FirstOrDefault(p => p.Name == DefaultPresetName) ??
                         manager.Presets.FirstOrDefault();
            if (active != null)
            {
                Owner._selectedPresetId = active.Id;
                Owner._selectedEntryId = active.Entries.FirstOrDefault()?.Id;
            }
        }

        float currentY = listingStandard.CurHeight;
        float availableHeight = Mathf.Max(300f, containerRect.height - currentY - 10f);

        Rect mainRect = listingStandard.GetRect(availableHeight);

        float leftPanelWidth = 200f;
        float panelGap = 4f;

        Pages.PromptList.DrawPresetListPanel(new Rect(mainRect.x, mainRect.y, leftPanelWidth, mainRect.height), manager);

        Rect rightPanelRect = new Rect(mainRect.x + leftPanelWidth + panelGap, mainRect.y,
            mainRect.width - (leftPanelWidth + panelGap), mainRect.height);
        Pages.PromptEditor.DrawEntryEditor(rightPanelRect, manager, settings);
    }
    internal static string LocalizePromptEntryName(string internalName)
    {
        return internalName switch
        {
            "Base Instruction" => "RimTalk.PromptEntry.BaseInstruction".Translate(),
            "EA Action Schema" => "RimTalk.PromptEntry.EAActionSchema".Translate(),
            "Схема дій EA" => "RimTalk.PromptEntry.EAActionSchema".Translate(),
            "JSON Format" => "RimTalk.PromptEntry.JsonFormat".Translate(),
            "Pawn Profiles" => "RimTalk.PromptEntry.PawnProfiles".Translate(),
            "Chat History" => "RimTalk.PromptEntry.ChatHistory".Translate(),
            "Memory & Knowledge Context" => "RimTalk.PromptEntry.MemoryKnowledge".Translate(),
            "Dialogue Prompt" => "RimTalk.PromptEntry.DialoguePrompt".Translate(),
            _ => internalName
        };
    }

    internal void UpdateSmartFilter(string text)
    {
        // Only trigger smart filter if we are in the Variables tab
        if (!Owner._showSidePanel || Owner._sidePanelMode != 0) return;

        // Try to get cursor position from Unity's TextEditor
        TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
        if (te == null || te.cursorIndex < 0) return;

        int pos = te.cursorIndex;
        if (pos > text.Length) pos = text.Length;

        // Look back from cursor to find the start of the current "word"
        int start = pos - 1;
        while (start >= 0 && !char.IsWhiteSpace(text[start]) && text[start] != '{' && text[start] != '}')
        {
            start--;
        }

        start++;

        string currentWord = "";
        if (start < pos)
        {
            currentWord = text.Substring(start, pos - start);
        }

        // Logic to decide when to update the search query
        bool shouldUpdate = false;
        
        // Check if we are inside brackets {{ ... }}
        int check = start - 1;
        while (check >= 0 && char.IsWhiteSpace(text[check])) check--;
        bool insideBrackets = (check >= 1 && text[check] == '{' && text[check-1] == '{');

        if (insideBrackets)
        {
            // 1. Always update if it contains a dot (property access)
            if (currentWord.Contains(".")) shouldUpdate = true;
            
            // 2. Update if length >= 2 (standard word)
            else if (currentWord.Length >= 2) shouldUpdate = true;
            
            // 3. Update (clear/shorten) if we are backspacing from a previously longer query
            else if (Owner._variableSearchQuery.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) && Owner._variableSearchQuery.Length > currentWord.Length)
            {
                shouldUpdate = true;
            }
            
            // 4. Always update if it's empty but inside brackets (to show all variables)
            else if (currentWord.Length == 0) shouldUpdate = true;
        }

        if (shouldUpdate)
        {
            Owner._variableSearchQuery = currentWord;
        }
    }

    internal void DrawPreviewContent(Rect rect, string content)
    {
        string text = PresetPreviewGenerator.GeneratePreview(content);
        Text.Font = GameFont.Small;

        Rect innerRect = rect.ContractedBy(5f);
        float viewWidth = innerRect.width - 16f;
        float height = Text.CalcHeight(text, viewWidth);

        Rect viewRect = new Rect(0f, 0f, viewWidth, height);

        Widgets.BeginScrollView(innerRect, ref Owner._previewScrollPos, viewRect);
        Widgets.TextArea(viewRect, text, readOnly: true);
        Widgets.EndScrollView();
    }
}
