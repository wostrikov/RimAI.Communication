using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// The default base instruction, and the defaults it replaced.
///
/// The instruction is stored twice - in the simple-mode setting and in the preset's Base
/// Instruction entry - and a stored copy that differs from the shipped default is read as
/// the player's own and copied back. A copy that is word for word a former default is the
/// player never having touched it, so it is upgraded to the current one; anything else
/// stays as the player wrote it.
/// </summary>
public static class BaseInstructionLineage
{
    /// <summary>What the dialogue language is written as in the canonical form.</summary>
    public const string LanguageToken = "{Lang}";

    /// <summary>
    /// SHA-256 of each former default in canonical form. The language a copy was stored with
    /// does not matter, nor does a superseded role line the copy still carries.
    /// </summary>
    private static readonly string[] FormerDefaultHashes =
    [
        // The default before pawns were told to talk to each other and never to the player.
        "36DD15E41870117A485CEEF66C2AF844223DEEC41FEAF146E1CEEF9A96912FAA",
    ];

    // "(лише мовою українська, 1–2 речення)": the one place the default names a language.
    private static readonly Regex LanguageSlot = new(@"(\(лише мовою )([^,\n]+)(, 1–2 речення\))");

    public static string Ukrainian(string lang) =>
        $"""
         Відігравай персонажа RimWorld відповідно до його профілю

         Правила:
         Зберігай оригінальні імена (не перекладай їх)
         Веди короткий діалог (лише мовою {lang}, 1–2 речення)
         Персонажі говорять між собою й ніколи не звертаються до гравця чи читача; жодної авторської оповіді

         Ролі:
         Полонений: насторожений і нерішучий; згадуй ув'язнення; благай або торгуйся
         {CaptiveDemeanorPolicy.SlaveLineUkrainian}
         Відвідувач: ввічливий, допитливий і шанобливий; стався до інших відвідувачів із тієї самої групи як до супутників
         Ворог: ворожий та агресивний; використовуй стислі накази й погрози

         Монолог = 1 репліка. Розмова = 4–8 коротких реплік
         """;

    /// <summary>True when <paramref name="value"/> is a former default nobody edited.</summary>
    public static bool IsFormerDefault(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Array.IndexOf(FormerDefaultHashes, Hash(Canonical(value))) >= 0;
    }

    /// <summary>
    /// The text with line endings unified, the ends trimmed, superseded role lines replaced
    /// and the dialogue language written as <see cref="LanguageToken"/>.
    /// </summary>
    public static string Canonical(string text)
    {
        string normalized = CaptiveDemeanorPolicy.ReplaceSupersededRoleLines((text ?? string.Empty).Replace("\r\n", "\n").Trim());
        return LanguageSlot.Replace(normalized, "${1}" + LanguageToken + "${3}");
    }

    /// <summary>
    /// The dialogue language a stored default was written for, or null. A copy is upgraded in
    /// its own language: settings load before the game's language is known, and the default
    /// read at that moment can name the wrong one.
    /// </summary>
    public static string LanguageOf(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var match = LanguageSlot.Match(text);
        return match.Success ? match.Groups[2].Value.Trim() : null;
    }

    /// <summary>The current default in the language <paramref name="stored"/> was written for, else in <paramref name="fallbackLang"/>.</summary>
    public static string Upgrade(string stored, string fallbackLang) => Ukrainian(LanguageOf(stored) ?? fallbackLang);

    public static string Hash(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", "");
    }
}
