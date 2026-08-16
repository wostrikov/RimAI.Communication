using System;
using System.Security.Cryptography;
using System.Text;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication.Data;

public static class Constant
{
    private const string UkrainianLegacyDefaultSha256 = "A7DE62B0FFA691ED0B9FAD337BB5F8C02B4A1A60A0087143BD541F796FCA4694";

    public static string LegacyEnglishDefaultInstruction =>
        $"""
         Role-play RimWorld character per profile

         Rules:
         Preserve original names (no translation)
         Keep dialogue short ({Lang} only, 1-2 sentences)

         Roles:
         Prisoner: wary, hesitant; mention confinement; plead or bargain
         Slave: fearful, obedient; reference forced labor and exhaustion; call colonists "master"
         Visitor: polite, curious, deferential; treat other visitors in the same group as companions
         Enemy: hostile, aggressive; terse commands/threats

         Monologue = 1 turn. Conversation = 4-8 short turns
         """;

    public static bool IsLegacyDefaultInstruction(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        static string Normalize(string text) => (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        string normalized = Normalize(value);
        if (normalized == Normalize(LegacyEnglishDefaultInstruction)) return true;
        using var sha = SHA256.Create();
        string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", "");
        return string.Equals(hash, UkrainianLegacyDefaultSha256, StringComparison.Ordinal);
    }

    public const string LegacyEnglishJsonInstruction = """
                                                        Output JSONL.
                                                        Required keys: "name", "text".
                                                        """;

    public const string LegacyEnglishSocialInstruction = """
                                                          Optional keys (Include only if social interaction occurs):
                                                          "act": Insult, Slight, Chat, Kind
                                                          "target": targetName
                                                          """;
    public const string DefaultCloudModel = "gemma-4-26b-a4b-it";
    public const string FallbackCloudModel = "gemma-4-31b-it";
    public const string ChooseModel = "(choose model)";

    public static string Lang =>
        DialogueLanguage.TryGetNativeName(out var name)
            ? name
            : LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English";
    public static HediffDef VocalLinkDef => DefDatabase<HediffDef>.GetNamedSilentFail("VocalLinkImplant");

    public static string DefaultInstruction =>
        $"""
         Відігравай персонажа RimWorld відповідно до його профілю

         Правила:
         Зберігай оригінальні імена (не перекладай їх)
         Веди короткий діалог (лише мовою {Lang}, 1–2 речення)

         Ролі:
         Полонений: насторожений і нерішучий; згадуй ув'язнення; благай або торгуйся
         Раб: наляканий і слухняний; згадуй примусову працю та виснаження; називай колоністів «господарями»
         Відвідувач: ввічливий, допитливий і шанобливий; стався до інших відвідувачів із тієї самої групи як до супутників
         Ворог: ворожий та агресивний; використовуй стислі накази й погрози

         Монолог = 1 репліка. Розмова = 4–8 коротких реплік
         """;

    public const string JsonInstruction = """
                                           Виводь JSONL.
                                           Обов'язкові ключі: "name", "text".
                                           """;
    
    public const string SocialInstruction = """
                                           Необов'язкові ключі (додавай лише за наявності соціальної взаємодії):
                                           "act": Insult, Slight, Chat, Kind
                                           "target": targetName
                                           """;

    // Get the current instruction from settings or fallback to default, always append JSON instruction
    // NOTE: This is now primarily used as a fallback. The new PromptManager system is preferred.
    public static string Instruction
    {
        get
        {
            var settings = Settings.Get();
            var baseInstruction = GetBaseInstruction();
        
            return baseInstruction + "\n" + JsonInstruction + (settings.ApplyMoodAndSocialEffects ? "\n" + SocialInstruction : "");
        }
    }

    private static string GetBaseInstruction()
    {
        var preset = PromptManager.Instance?.GetActivePreset();
        if (preset == null) return DefaultInstruction;

        var entry = preset.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, "Base Instruction", StringComparison.OrdinalIgnoreCase))
                    ?? preset.Entries.FirstOrDefault(e =>
                        e.Role == PromptRole.System && e.Position == PromptPosition.Relative);

        return string.IsNullOrWhiteSpace(entry?.Content) ? DefaultInstruction : entry.Content;
    }
    
    // JSON instruction for use by PromptManager
    public static string GetJsonInstruction(bool includeSocialEffects)
    {
        return JsonInstruction + (includeSocialEffects ? "\n" + SocialInstruction : "");
    }

    public static string PersonaGenInstruction =>
        $"""
         Створи кумедну персону мовою {Lang} для використання як стилю розмови. Опис має складатися з одного короткого речення.
         Укажи манеру мовлення, основне ставлення та одну дивну рису, що робить персонажа незабутнім.
         Пиши конкретно й сміливо, уникай нудних рис.
         Також визнач балакучість: 0.1–0.3 (мовчазний), 0.4–0.7 (звичайний), 0.8–1.0 (балакучий).
         Повертай лише JSON із полями 'persona' (string) і 'chattiness' (float).
         """;

    private static PersonalityData[] _personalities;
    public static PersonalityData[] Personalities => _personalities ??=
    [
        new("RimTalk.Persona.CheerfulHelper".Translate(), 0.75f),
        new("RimTalk.Persona.CynicalRealist".Translate(), 0.4f),
        new("RimTalk.Persona.ShyThinker".Translate(), 0.15f),
        new("RimTalk.Persona.Hothead".Translate(), 0.6f),
        new("RimTalk.Persona.Philosopher".Translate(), 0.8f),
        new("RimTalk.Persona.DarkHumorist".Translate(), 0.7f),
        new("RimTalk.Persona.Caregiver".Translate(), 0.75f),
        new("RimTalk.Persona.Opportunist".Translate(), 0.65f),
        new("RimTalk.Persona.OptimisticDreamer".Translate(), 0.8f),
        new("RimTalk.Persona.Pessimist".Translate(), 0.35f),
        new("RimTalk.Persona.StoicSoldier".Translate(), 0.2f),
        new("RimTalk.Persona.FreeSpirit".Translate(), 0.85f),
        new("RimTalk.Persona.Workaholic".Translate(), 0.25f),
        new("RimTalk.Persona.Slacker".Translate(), 0.55f),
        new("RimTalk.Persona.NobleIdealist".Translate(), 0.75f),
        new("RimTalk.Persona.StreetwiseSurvivor".Translate(), 0.5f),
        new("RimTalk.Persona.Scholar".Translate(), 0.8f),
        new("RimTalk.Persona.Jokester".Translate(), 0.9f),
        new("RimTalk.Persona.MelancholicPoet".Translate(), 0.2f),
        new("RimTalk.Persona.Paranoid".Translate(), 0.3f),
        new("RimTalk.Persona.Commander".Translate(), 0.5f),
        new("RimTalk.Persona.Coward".Translate(), 0.35f),
        new("RimTalk.Persona.ArrogantNoble".Translate(), 0.7f),
        new("RimTalk.Persona.LoyalCompanion".Translate(), 0.65f),
        new("RimTalk.Persona.CuriousExplorer".Translate(), 0.85f),
        new("RimTalk.Persona.ColdRationalist".Translate(), 0.15f),
        new("RimTalk.Persona.FlirtatiousCharmer".Translate(), 0.95f),
        new("RimTalk.Persona.BitterOutcast".Translate(), 0.25f),
        new("RimTalk.Persona.Zealot".Translate(), 0.9f),
        new("RimTalk.Persona.Trickster".Translate(), 0.8f),
        new("RimTalk.Persona.DeadpanRealist".Translate(), 0.3f),
        new("RimTalk.Persona.ChildAtHeart".Translate(), 0.85f),
        new("RimTalk.Persona.SkepticalScientist".Translate(), 0.6f),
        new("RimTalk.Persona.Martyr".Translate(), 0.65f),
        new("RimTalk.Persona.Manipulator".Translate(), 0.75f),
        new("RimTalk.Persona.Rebel".Translate(), 0.7f),
        new("RimTalk.Persona.Oddball".Translate(), 0.6f),
        new("RimTalk.Persona.GreedyMerchant".Translate(), 0.85f),
        new("RimTalk.Persona.Romantic".Translate(), 0.8f),
        new("RimTalk.Persona.BattleManiac".Translate(), 0.4f),
        new("RimTalk.Persona.GrumpyElder".Translate(), 0.5f),
        new("RimTalk.Persona.AmbitiousClimber".Translate(), 0.75f),
        new("RimTalk.Persona.Mediator".Translate(), 0.7f),
        new("RimTalk.Persona.Gambler".Translate(), 0.75f),
        new("RimTalk.Persona.ArtisticSoul".Translate(), 0.45f),
        new("RimTalk.Persona.Drifter".Translate(), 0.3f),
        new("RimTalk.Persona.Perfectionist".Translate(), 0.4f),
        new("RimTalk.Persona.Vengeful".Translate(), 0.35f)
    ];

    public static void ReplacePersonalities(PersonalityData[] personalities)
    {
        if (personalities == null || personalities.Length == 0)
            return;
        _personalities = personalities;
    }

    private static PersonalityData _personaAnimal;
    public static PersonalityData PersonaAnimal => _personaAnimal ??= new("RimTalk.Persona.Animal".Translate(), 0.2f);

    private static PersonalityData _personaMech;
    public static PersonalityData PersonaMech => _personaMech ??= new("RimTalk.Persona.Mech".Translate(), 0.2f);

    private static PersonalityData _personaNonHuman;
    public static PersonalityData PersonaNonHuman => _personaNonHuman ??= new("RimTalk.Persona.NonHuman".Translate(), 0.2f);
}
