using System;
using System.Text;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// How a letter or message becomes the prompt of a talk about it: the letter's own label as the
/// subject, and its text cut short. A full quest description ran to a screen of text, and the
/// model retold it instead of reacting to it. Host-free, so it can be tested without the game.
/// </summary>
public static class EventPromptPolicy
{
    public const int MaxDescriptionLength = 140;

    /// <summary>
    /// "(instruction: label)\n[description]". A description that repeats the label at its start
    /// loses it there, and one that is nothing but the label - a message's is - keeps it and
    /// goes without the label in the head.
    /// </summary>
    public static string Compose(string instruction, string label, string description, string suffix = "")
    {
        string text = Collapse(description);
        label = Collapse(label);
        if (label.Length > 0 && text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            text = text.Substring(label.Length).TrimStart(' ', ':', '-', '.');
        if (text.Length == 0)
        {
            text = label;
            label = string.Empty;
        }

        string head = label.Length > 0 ? $"({instruction}: {label})" : $"({instruction})";
        return $"{head}\n[{Shorten(text)}{suffix}]";
    }

    /// <summary>
    /// The description cut to <see cref="MaxDescriptionLength"/>: at the last sentence end that
    /// fits, else at the last word that fits, with an ellipsis.
    /// </summary>
    public static string Shorten(string description, int maxLength = MaxDescriptionLength)
    {
        string text = Collapse(description);
        if (text.Length <= maxLength) return text;

        int sentenceEnd = -1;
        for (int i = maxLength - 1; i >= maxLength / 3; i--)
        {
            if (IsFullWidthSentenceEnd(text[i])
                || IsSentenceEnd(text[i]) && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                sentenceEnd = i;
                break;
            }
        }
        if (sentenceEnd >= 0) return text.Substring(0, sentenceEnd + 1);

        int cut = text.LastIndexOf(' ', maxLength - 1);
        if (cut < maxLength / 3) cut = maxLength - 1;
        return text.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "…";
    }

    private static bool IsSentenceEnd(char c) => c is '.' or '!' or '?' or '…';

    // Chinese and Japanese put no space after a sentence.
    private static bool IsFullWidthSentenceEnd(char c) => c is '。' or '！' or '？';

    /// <summary>Paragraphs and runs of spaces become single spaces.</summary>
    private static string Collapse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space) sb.Append(' ');
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
