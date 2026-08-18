using System;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Communication.Util;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication;

internal sealed class CommunicationAiInstructionSettingsPage : CommunicationSettingsCollaborator
{
    internal CommunicationAiInstructionSettingsPage(Settings owner) : base(owner) { }

    internal void DrawAIInstructionSettings(Listing_Standard listingStandard, bool showAdvancedSwitch = false)
    {
        CommunicationSettings settings = Settings.Get();

        bool isSimpleMode = !settings.UseAdvancedPromptMode;

        PromptEntry baseEntry = null;
        string currentContent;

        if (isSimpleMode)
        {
            currentContent = settings.SimpleModeInstruction;
            if (string.IsNullOrWhiteSpace(currentContent))
            {
                currentContent = Constant.DefaultInstruction;
                settings.SimpleModeInstruction = currentContent;
            }
        }
        else
        {
            var manager = PromptManager.Instance;
            manager.EnsureInitialized();
            var activePreset = manager.GetActivePreset();
            baseEntry = GetOrCreateBaseInstructionEntry(activePreset);
            currentContent = baseEntry?.Content ?? Constant.DefaultInstruction;

            if (Owner._aiInstructionPresetId != (activePreset?.Id ?? ""))
            {
                Owner._textAreaInitialized = false;
                Owner._aiInstructionPresetId = activePreset?.Id ?? "";
            }
        }

        if (!Owner._textAreaInitialized)
        {
            Owner._textAreaBuffer = currentContent;
            Owner._textAreaInitialized = true;
        }

        var activeConfig = settings.GetActiveConfig();
        var modelName = activeConfig?.SelectedModel ?? "N/A";
        var aiInstructionPrompt = "RimTalk.Settings.AIInstructionPrompt".Translate(modelName);

        float textHeight = Text.CalcHeight(aiInstructionPrompt,
            listingStandard.ColumnWidth - (showAdvancedSwitch ? 180f : 0f));
        float headerHeight = Mathf.Max(textHeight, 30f);

        Rect headerRect = listingStandard.GetRect(headerHeight);

        if (showAdvancedSwitch)
        {
            float buttonWidth = 170f;
            Rect buttonRect = new Rect(headerRect.xMax - buttonWidth, headerRect.y, buttonWidth, 28f);

            if (Widgets.ButtonText(buttonRect, "RimTalk.Settings.SwitchToAdvancedSettings".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk.Settings.AdvancedModeWarning".Translate(),
                    () =>
                    {
                        settings.UseAdvancedPromptMode = true;
                        Owner._textAreaInitialized = false;
                        Owner._aiInstructionPresetId = "";
                    }));
            }

            Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - buttonWidth - 10f,
                headerRect.height);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, aiInstructionPrompt);
            Text.Anchor = TextAnchor.UpperLeft;
        }
        else
        {
            Widgets.Label(headerRect, aiInstructionPrompt);
        }

        listingStandard.Gap(6f);

        // Context information tip
        Text.Font = GameFont.Tiny;
        GUI.color = Color.green;
        Rect contextTipRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(contextTipRect, "RimTalk.Settings.AutoIncludedTip".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        listingStandard.Gap(6f);

        // Warning about rate limits
        Text.Font = GameFont.Tiny;
        GUI.color = Color.yellow;
        Rect rateLimitRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(rateLimitRect, "RimTalk.Settings.RateLimitWarning".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        listingStandard.Gap(6f);

        // Token info display
        int currentTokens = CommonUtil.EstimateTokenCount(Owner._textAreaBuffer);
        int maxAllowedTokens = CommonUtil.GetMaxAllowedTokens(settings.TalkInterval);
        string tokenInfo = "RimTalk.Settings.TokenInfo".Translate(currentTokens, maxAllowedTokens);

        GUI.color = currentTokens > maxAllowedTokens ? Color.red : Color.green;

        Text.Font = GameFont.Tiny;
        Rect tokenInfoRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(tokenInfoRect, tokenInfo);
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        listingStandard.Gap(6f);

        float textAreaHeight = 350f;
        Rect textAreaRect = listingStandard.GetRect(textAreaHeight);

        float innerWidth = textAreaRect.width - 16f;
        float contentHeight = Mathf.Max(textAreaHeight, Text.CalcHeight(Owner._textAreaBuffer, innerWidth) + 40f);
        Rect viewRect = new Rect(0f, 0f, innerWidth, contentHeight);

        const string controlName = "RimTalk_AIInstruction_TextArea";
        Widgets.BeginScrollView(textAreaRect, ref Owner._aiInstructionScrollPos, viewRect);
        GUI.SetNextControlName(controlName);
        string newInstruction = Widgets.TextArea(new Rect(0f, 0f, innerWidth, contentHeight), Owner._textAreaBuffer);

        // Auto-scroll logic: only scroll if the cursor position changed
        if (GUI.GetNameOfFocusedControl() == controlName)
        {
            TextEditor te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te != null && te.cursorIndex != Owner._lastTextAreaCursorPos)
            {
                Owner._lastTextAreaCursorPos = te.cursorIndex;
                float cursorY = te.graphicalCursorPos.y;
                if (cursorY < Owner._aiInstructionScrollPos.y)
                    Owner._aiInstructionScrollPos.y = cursorY;
                else if (cursorY + 25f > Owner._aiInstructionScrollPos.y + textAreaHeight)
                    Owner._aiInstructionScrollPos.y = cursorY + 25f - textAreaHeight;
            }
        }

        Widgets.EndScrollView();

        if (newInstruction != Owner._textAreaBuffer)
        {
            Owner._textAreaBuffer = newInstruction;

            // Write back to correct target
            if (isSimpleMode)
            {
                settings.SimpleModeInstruction = newInstruction;
                var manager = PromptManager.Instance;
                var activePreset = manager.GetActivePreset();
                var activeBaseEntry = GetOrCreateBaseInstructionEntry(activePreset);
                if (activeBaseEntry != null)
                {
                    activeBaseEntry.Content = newInstruction;
                }
            }
            else if (baseEntry != null)
            {
                baseEntry.Content = newInstruction;
            }
        }

        listingStandard.Gap(6f);

        Rect resetButtonRect = listingStandard.GetRect(30f);
        if (Widgets.ButtonText(resetButtonRect, "RimTalk.Settings.ResetToDefault".Translate()))
        {
            Owner._textAreaBuffer = Constant.DefaultInstruction;

            if (isSimpleMode)
            {
                settings.SimpleModeInstruction = Constant.DefaultInstruction;
                // Also update active preset
                var manager = PromptManager.Instance;
                var activePreset = manager.GetActivePreset();
                var activeBaseEntry = GetOrCreateBaseInstructionEntry(activePreset);
                if (activeBaseEntry != null)
                {
                    activeBaseEntry.Content = Constant.DefaultInstruction;
                }
            }

            listingStandard.Gap(10f);
        }
    }

    internal static PromptEntry GetOrCreateBaseInstructionEntry(PromptPreset preset)
    {
        if (preset == null) return null;

        var entry = preset.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "Base Instruction", StringComparison.OrdinalIgnoreCase));
        if (entry != null) return entry;

        entry = preset.Entries.FirstOrDefault(e =>
            e.Role == PromptRole.System && e.Position == PromptPosition.Relative);
        if (entry != null) return entry;

        entry = new PromptEntry
        {
            Name = "Base Instruction",
            Role = PromptRole.System,
            Position = PromptPosition.Relative,
            Content = Constant.DefaultInstruction
        };
        preset.Entries.Insert(0, entry);
        return entry;
    }

}
