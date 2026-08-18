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

internal sealed class DebugWindowDetailsPanel : DebugWindowCollaborator
{
    internal DebugWindowDetailsPanel(DebugWindow owner) : base(owner) { }
    internal void InitializeContextStyle()
    {
        if (Owner._contextStyle == null)
        {
            Owner._contextStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Tiny])
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
        }

        if (Owner._monoTinyStyle == null)
        {
            Owner._monoTinyStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Tiny])
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
        }
    }
    internal void DrawDetailsPanel(Rect rect)
    {
        Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f, 0.8f));
        InitializeContextStyle();

        var inner = rect.ContractedBy(8f);
        GUI.BeginGroup(inner);

        float y = 0f;
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(0f, y, inner.width, 24f), "RimTalk.DebugWindow.Details".Translate());
        y += 26f;

        if (Owner._selectedLog == null)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, inner.width, 50f), "RimTalk.DebugWindow.SelectRowHint".Translate());
            GUI.color = Color.white;
            GUI.EndGroup();
            return;
        }

        // Initialize or update temp strings if a new row is selected OR if the selected row is still generating
        bool isGenerating = Owner._selectedLog.Response == null || AIService.IsBusy(); 
        if (Owner._selectedLog.Id != Owner._selectedRequestIdForTemp || isGenerating)
        {
            if (Owner._selectedLog.Id != Owner._selectedRequestIdForTemp)
            {
                Owner._selectedRequestIdForTemp = Owner._selectedLog.Id;
                Owner._expandedPromptSegmentIndices.Clear();
            }
            
            Owner._tempResponse = Owner._selectedLog.Response ?? string.Empty;
            Owner._tempPromptSegments = ResolvePromptSegments(Owner._selectedLog);
            Owner._tempPromptSegmentsText = FormatPromptSegments(Owner._tempPromptSegments);
        }

        var header = new StringBuilder();
        header.Append(Owner._selectedLog.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
        header.Append("  |  ");
        header.Append((Owner._selectedLog.Name ?? "-").Trim());
        if (Owner._selectedLog.InteractionType != null)
        {
            header.Append("  |  ");
            header.Append(Owner._selectedLog.InteractionType);
        }

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Widgets.Label(new Rect(0f, y, inner.width, 18f), header.ToString());
        GUI.color = Color.white;
        y += 22f;

        float buttonsRowH = 24f;
        float btnW = 88f;
        float btnX = 0f;

        // Copy All Button
        if (Widgets.ButtonText(new Rect(btnX, y, btnW, buttonsRowH), "RimTalk.DebugWindow.CopyAll".Translate()))
        {
            GUIUtility.systemCopyBuffer = Owner._selectedLog.ToString();
            Messages.Message("RimTalk.DebugWindow.Copied".Translate(), MessageTypeDefOf.TaskCompletion, false);
        }

        // API Log Button
        if (Owner._selectedLog.GetState() != State.None)
        {
            btnX += btnW + 6f;
            Rect reportRect = new Rect(btnX, y, btnW, buttonsRowH);
            if (Widgets.ButtonText(reportRect, "RimTalk.DebugWindow.ApiLog".Translate()))
            {
                GUIUtility.systemCopyBuffer = Owner._selectedLog.Payload?.ToString();
                Messages.Message("RimTalk.DebugWindow.Copied".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }
        }

        // Resend Button
        GUI.enabled = Owner._selectedLog.Channel != Channel.User;
        btnX += btnW + 6f;
        var prevResendColor = GUI.color;
        GUI.color = new Color(0.6f, 0.9f, 0.6f);
        Rect resendRect = new Rect(btnX, y, btnW, buttonsRowH);
        if (Widgets.ButtonText(resendRect, "RimTalk.DebugWindow.Resend".Translate()))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            Resend();
        }

        TooltipHandler.TipRegion(resendRect, "RimTalk.DebugWindow.ResendTooltip".Translate());
        GUI.color = prevResendColor;
        GUI.enabled = true;

        y += buttonsRowH + 8f;

        if (Owner._selectedLog.GetState() == State.Failed)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.5f, 0.5f);
            string failedMsg = "RimTalk.DebugWindow.FailMsg".Translate();
            Widgets.Label(new Rect(0f, y, inner.width, 20f), failedMsg);
            GUI.color = prevColor;
            y += 30f;
        }

        // Scrollable selectable areas
        var scrollOuter = new Rect(0f, y, inner.width, inner.height - y);
        float blockSpacing = 10f;
        float headerH = 18f;

        // Calculate heights dynamically based on current content
        float viewWidth = scrollOuter.width - 16f;
        float textAreaWidth = viewWidth - 8f;
        float respH = Mathf.Max(40f,
            Owner._monoTinyStyle.CalcHeight(new GUIContent(Owner._tempResponse), textAreaWidth) + 10f);
        float msgH = CalculatePromptSegmentsHeight(Owner._tempPromptSegments, viewWidth);

        var viewH = headerH + respH + blockSpacing +
                    msgH + 10f;

        var view = new Rect(0f, 0f, scrollOuter.width - 16f, viewH);

        Widgets.BeginScrollView(scrollOuter, ref Owner._detailsScrollPosition, view);
        float yy = 0f;

        // Response Block
        DrawSelectableBlock(ref yy, view.width, "RimTalk.DebugWindow.Response".Translate(),
            ref Owner._tempResponse, respH, DebugWindow.ControlNameDetailResponse,
            onCopy: () =>
            {
                GUIUtility.systemCopyBuffer = Owner._tempResponse;
                Messages.Message("RimTalk.DebugWindow.Copied".Translate(), MessageTypeDefOf.TaskCompletion, false);
            },
            readOnly: true);
        yy += blockSpacing;

        // Prompt Messages Block (segmented, collapsible)
        DrawPromptMessagesBlock(ref yy, view.width, "RimTalk.DebugWindow.PromptMessages".Translate(),
            Owner._tempPromptSegments, Owner._tempPromptSegmentsText);

        Widgets.EndScrollView();

        GUI.EndGroup();
    }

    internal void DrawSelectableBlock(ref float y, float width, string title, ref string content, float contentHeight,
        string controlName, Action onCopy, Action onReset = null, bool readOnly = false)
    {
        // Header Row: Label + Icons
        float headerHeight = 18f;

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;

        Vector2 labelSize = Text.CalcSize(title);
        Rect labelRect = new Rect(0f, y, labelSize.x, headerHeight);
        Widgets.Label(labelRect, title);

        // Draw Copy Icon next to label
        Rect copyRect = new Rect(labelRect.xMax + 8f, y, 16f, 16f);
        if (Widgets.ButtonImage(copyRect, TexButton.Copy))
        {
            onCopy?.Invoke();
        }

        TooltipHandler.TipRegion(copyRect, "RimTalk.DebugWindow.Copy".Translate());

        // Draw Reset Icon next to Copy Icon
        if (onReset != null)
        {
            Rect resetRect = new Rect(copyRect.xMax + 4f, y, 16f, 16f);
            if (Widgets.ButtonImage(resetRect, TexButton.HotReloadDefs))
            {
                onReset.Invoke();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            TooltipHandler.TipRegion(resetRect, "RimTalk.DebugWindow.Undo".Translate());
        }

        GUI.color = Color.white;
        y += headerHeight;

        // Content Box (Editable)
        var box = new Rect(0f, y, width, contentHeight);

        bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
        Color colorUnfocused = new Color(0.05f, 0.05f, 0.05f, 0.55f);
        Color colorFocused = new Color(0.15f, 0.15f, 0.15f, 0.4f);
        Widgets.DrawBoxSolid(box, isFocused && !readOnly ? colorFocused : colorUnfocused);

        var textRect = box.ContractedBy(4f);
        GUI.SetNextControlName(controlName);

        if (readOnly)
            GUI.TextArea(textRect, content, Owner._monoTinyStyle);
        else
            content = GUI.TextArea(textRect, content, Owner._monoTinyStyle);

        y += contentHeight;
    }

    internal void DrawPromptMessagesBlock(ref float y, float width, string title,
        List<PromptMessageSegment> segments, string combinedText)
    {
        float headerHeight = 18f;

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;

        Vector2 labelSize = Text.CalcSize(title);
        Rect labelRect = new Rect(0f, y, labelSize.x, headerHeight);
        Widgets.Label(labelRect, title);

        Rect copyRect = new Rect(labelRect.xMax + 8f, y, 16f, 16f);
        if (Widgets.ButtonImage(copyRect, TexButton.Copy))
        {
            GUIUtility.systemCopyBuffer = combinedText ?? "";
            Messages.Message("RimTalk.DebugWindow.Copied".Translate(), MessageTypeDefOf.TaskCompletion, false);
        }

        TooltipHandler.TipRegion(copyRect, "RimTalk.DebugWindow.Copy".Translate());

        GUI.color = Color.white;
        y += headerHeight;

        if (segments == null || segments.Count == 0)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, y, width, 20f), "-");
            GUI.color = Color.white;
            y += 20f;
            return;
        }

        const float messageHeaderHeight = 22f;
        const float messageSpacing = 6f;

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            string preview = GetPromptMessagePreview(segment.Content);
            string roleLabel = GetRoleLabel(segment.Role);
            string entryName = string.IsNullOrWhiteSpace(segment.EntryName) ? "Entry" : segment.EntryName;
            string state = Owner._expandedPromptSegmentIndices.Contains(i) ? "[-]" : "[+]";
            string label = $"{state} {i + 1}. {entryName} ({roleLabel}): {preview}";

            var headerRect = new Rect(0f, y, width, messageHeaderHeight);
            Widgets.DrawBoxSolid(headerRect, new Color(0.12f, 0.12f, 0.12f, 0.6f));
            Widgets.Label(new Rect(headerRect.x + 6f, headerRect.y + 2f, headerRect.width - 12f, headerRect.height),
                label);

            if (Widgets.ButtonInvisible(headerRect))
            {
                if (Owner._expandedPromptSegmentIndices.Contains(i))
                    Owner._expandedPromptSegmentIndices.Remove(i);
                else
                    Owner._expandedPromptSegmentIndices.Add(i);
            }

            y += messageHeaderHeight;

            if (Owner._expandedPromptSegmentIndices.Contains(i))
            {
                string safeContent = segment.Content ?? "";
                float bodyHeight = Mathf.Max(40f,
                    Owner._monoTinyStyle.CalcHeight(new GUIContent(safeContent), width - 8f) + 10f);
                var bodyRect = new Rect(0f, y, width, bodyHeight);
                Widgets.DrawBoxSolid(bodyRect, new Color(0.05f, 0.05f, 0.05f, 0.55f));

                var textRect = bodyRect.ContractedBy(4f);
                string newContent = GUI.TextArea(textRect, safeContent, Owner._monoTinyStyle);
                if (newContent != safeContent)
                {
                    segment.Content = newContent;
                    Owner._tempPromptSegmentsText = FormatPromptSegments(segments);
                }
                y += bodyHeight;
            }

            y += messageSpacing;
        }
    }
    internal static List<PromptMessageSegment> ResolvePromptSegments(ApiLog log)
    {
        var request = log?.TalkRequest;
        if (request?.PromptMessageSegments != null && request.PromptMessageSegments.Count > 0)
            return request.PromptMessageSegments;

        var segments = new List<PromptMessageSegment>();
        if (request == null) return segments;

        if (request.PromptMessages != null && request.PromptMessages.Count > 0)
        {
            for (int i = 0; i < request.PromptMessages.Count; i++)
            {
                var (role, content) = request.PromptMessages[i];
                segments.Add(new PromptMessageSegment($"message-{i}", $"{"RimTalk.DebugWindow.FormatEntry".Translate()} {i + 1}", role, content));
            }
            return segments;
        }

        var instruction = $"{Constant.Instruction}\n{request.Context}";
        segments.Add(new PromptMessageSegment("system-instruction", "RimTalk.DebugWindow.SystemInstruction".Translate(), Role.System, instruction));

        if (request.Initiator != null)
        {
            foreach (var (role, message) in TalkHistory.GetMessageHistory(request.Initiator))
            {
                segments.Add(new PromptMessageSegment("chat-history", "RimTalk.DebugWindow.ChatHistory".Translate(), role, message));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
            segments.Add(new PromptMessageSegment("input-prompt", "RimTalk.DebugWindow.InputPrompt".Translate(), Role.User, request.Prompt));

        return segments;
    }

    internal float CalculatePromptSegmentsHeight(List<PromptMessageSegment> segments, float width)
    {
        float height = 18f;
        if (segments == null || segments.Count == 0)
            return height + 20f;

        const float messageHeaderHeight = 22f;
        const float messageSpacing = 6f;

        for (int i = 0; i < segments.Count; i++)
        {
            height += messageHeaderHeight;
            if (Owner._expandedPromptSegmentIndices.Contains(i))
            {
                string content = segments[i].Content ?? "";
                height += Mathf.Max(40f,
                    Owner._monoTinyStyle.CalcHeight(new GUIContent(content), width - 8f) + 10f);
            }
            height += messageSpacing;
        }

        return height;
    }

    internal static string GetPromptMessagePreview(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return "(empty)";

        var firstLine = content.Replace("\r", "").Split('\n')[0].Trim();
        if (firstLine.Length > 80)
            firstLine = firstLine.Substring(0, 77) + "...";

        return firstLine;
    }

    internal static string GetRoleLabel(Role role)
    {
        return role == Role.AI ? "Assistant" : role.ToString();
    }

    /// <summary>
    /// Formats prompt segments into a readable string with entry and role headers.
    /// Example output:
    /// Entry: Base Instruction
    /// Role: system
    /// [content]
    /// </summary>
    internal static string FormatPromptSegments(List<PromptMessageSegment> segments)
    {
        if (segments == null || segments.Count == 0)
            return $"({"RimTalk.DebugWindow.SelectRowHint".Translate()})";

        var sb = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var roleLabel = segment.Role == Role.AI ? "assistant" : segment.Role.ToString().ToLowerInvariant();
            var entryName = string.IsNullOrWhiteSpace(segment.EntryName) ? "RimTalk.DebugWindow.FormatEntry".Translate().Resolve() : segment.EntryName;
            sb.Append("RimTalk.DebugWindow.FormatEntry".Translate());
            sb.Append(": ");
            sb.AppendLine(entryName);
            sb.Append("RimTalk.DebugWindow.FormatRole".Translate());
            sb.Append(": ");
            sb.AppendLine(roleLabel);
            sb.AppendLine(segment.Content ?? "");
            
            // Add blank line between messages (except after the last one)
            if (i < segments.Count - 1)
                sb.AppendLine();
        }
        return sb.ToString();
    }

    internal void Resend()
    {
        if (AIService.IsBusy())
        {
            Messages.Message("RimTalk.DebugWindow.ResendError".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }

        TalkRequest debugRequest = Owner._selectedLog.TalkRequest.Clone();
        
        // If we have modified segments in the UI, apply them to the resent request
        if (Owner._tempPromptSegments != null && Owner._tempPromptSegments.Count > 0)
        {
            debugRequest.PromptMessageSegments = Owner._tempPromptSegments.Select(s => new PromptMessageSegment(s.EntryId, s.EntryName, s.Role, s.Content)).ToList();
            debugRequest.PromptMessages = debugRequest.PromptMessageSegments.Select(s => (s.Role, s.Content)).ToList();
        }

        if (Owner._selectedLog.Channel == Channel.Stream)
            TalkService.GenerateTalkDebug(debugRequest);
        else if (Owner._selectedLog.Channel == Channel.Query)
            Task.Run(() => AIService.Query<PersonalityData>(debugRequest));

        Messages.Message("RimTalk.DebugWindow.ResendSuccess".Translate(), MessageTypeDefOf.TaskCompletion);
    }

    internal void Reset()
    {
        TalkHistory.Clear();
        TalkRequestPool.ClearHistory();
        Stats.Reset();
        ApiHistory.Clear();
        Owner.UpdateData();
        Messages.Message("RimTalk.DebugWindow.HistoryCleared".Translate(), MessageTypeDefOf.TaskCompletion, false);
    }
}
