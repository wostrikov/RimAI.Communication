using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Client.OpenAI;
using Ustas.RimAI.Communication.Client.Player2;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Configuration;
using Ustas.RimAI.Core.Threading;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Communication;

internal sealed class CommunicationApiSettingsPage : CommunicationSettingsCollaborator
{
    internal CommunicationApiSettingsPage(Settings owner) : base(owner) { }
    static readonly string[] SharedAiLanguageChoices =
        {
            "English",
            "Ukrainian",
            "ChineseSimplified",
            "ChineseTraditional",
            "Japanese",
            "Korean",
            "French",
            "German",
            "Spanish",
            "Russian",
            "Polish"
        };

    internal void DrawSharedAiSettings(Listing_Standard listingStandard)
    {
        if (!Settings.Get().UseSimpleConfig)
            DrawAdvancedApiSettings(listingStandard);
        else
            DrawSimpleApiSettings(listingStandard);
        DrawSharedAiLanguageSettings(listingStandard);
    }

    internal void DrawSharedAiLanguageSettings(Listing_Standard listingStandard)
    {
        CommunicationSettings settings = Settings.Get();
        listingStandard.Gap();
        listingStandard.Label("Ustas.RimAI.Settings.SharedAi.Language".Translate());
        string resolved = GameplayAiLanguage.Resolve(settings.GameplayAiLanguage, Settings.TryActiveGameLanguageEnglish());
        string current = string.IsNullOrWhiteSpace(settings.GameplayAiLanguage)
            ? "Ustas.RimAI.Settings.SharedAi.LanguageAuto".Translate(resolved)
            : resolved;
        Rect languageRect = listingStandard.GetRect(30f);
        if (Widgets.ButtonText(languageRect, current))
        {
            var options = new List<FloatMenuOption>
            {
                new("Ustas.RimAI.Settings.SharedAi.LanguageAuto".Translate(resolved), () => settings.GameplayAiLanguage = "")
            };
            foreach (var language in SharedAiLanguageChoices)
            {
                string choice = language;
                options.Add(new FloatMenuOption(choice, () => settings.GameplayAiLanguage = choice));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        listingStandard.Label("Ustas.RimAI.Settings.SharedAi.LanguageTooltip".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
    }

    internal void DrawSimpleApiSettings(Listing_Standard listingStandard)
    {
        CommunicationSettings settings = Settings.Get();

        // API Key section
        listingStandard.Label("RimTalk.Settings.GoogleApiKeyLabel".Translate());

        const float buttonWidth = 150f;
        const float spacing = 5f;

        Rect rowRect = listingStandard.GetRect(30f);
        rowRect.width -= buttonWidth + spacing;

        settings.SimpleApiKey = Widgets.TextField(rowRect, settings.SimpleApiKey);

        Rect buttonRect = new Rect(rowRect.xMax + spacing, rowRect.y, buttonWidth, rowRect.height);
        if (Widgets.ButtonText(buttonRect, "RimTalk.Settings.GetFreeApiKeyButton".Translate()))
        {
            Application.OpenURL("https://aistudio.google.com/app/apikey");
        }

        // Add description for free Google providers
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Rect cloudDescRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(cloudDescRect, "RimTalk.Settings.GoogleApiKeyDesc".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        listingStandard.Gap();

        // Show Advanced Settings button
        Rect advancedButtonRect = listingStandard.GetRect(30f);
        if (Widgets.ButtonText(advancedButtonRect, "RimTalk.Settings.SwitchToAdvancedSettings".Translate()))
        {
            settings.UseSimpleConfig = false;
        }
    }

    internal void DrawAdvancedApiSettings(Listing_Standard listingStandard)
    {
        CommunicationSettings settings = Settings.Get();

        // Show Simple Settings button
        Rect simpleButtonRect = listingStandard.GetRect(30f);
        if (Widgets.ButtonText(simpleButtonRect, "RimTalk.Settings.SwitchToSimpleSettings".Translate()))
        {
            if (string.IsNullOrWhiteSpace(settings.SimpleApiKey))
            {
                var firstValidCloudConfig = settings.CloudConfigs.FirstOrDefault(c => c.IsValid());
                if (firstValidCloudConfig != null)
                {
                    settings.SimpleApiKey = firstValidCloudConfig.ApiKey;
                }
            }
            settings.UseSimpleConfig = true;
        }

        listingStandard.Gap();

        // Cloud providers option with description
        Rect radioRect1 = listingStandard.GetRect(24f);
        if (Widgets.RadioButtonLabeled(radioRect1, "RimTalk.Settings.CloudProviders".Translate(), settings.UseCloudProviders))
        {
            settings.UseCloudProviders = true;
        }

        // Add description for cloud providers
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Rect cloudDescRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(cloudDescRect, "RimTalk.Settings.CloudProvidersDesc".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        listingStandard.Gap(3f);

        // Local provider option with description
        Rect radioRect2 = listingStandard.GetRect(24f);
        if (Widgets.RadioButtonLabeled(radioRect2, "RimTalk.Settings.LocalProvider".Translate(), !settings.UseCloudProviders))
        {
            settings.UseCloudProviders = false;
            settings.LocalConfig.Provider = AIProvider.Local;
        }

        // Add description for local provider
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Rect localDescRect = listingStandard.GetRect(Text.LineHeight);
        Widgets.Label(localDescRect, "RimTalk.Settings.LocalProviderDesc".Translate());
        GUI.color = Color.white;
        Text.Font = GameFont.Small;

        listingStandard.Gap();

        // Draw appropriate section based on selection
        if (settings.UseCloudProviders)
        {
            Pages.CloudApi.DrawCloudProvidersSection(listingStandard, settings);
        }
        else
        {
            DrawLocalProviderSection(listingStandard, settings);
        }
    }

    internal void DrawLocalProviderSection(Listing_Standard listingStandard, CommunicationSettings settings)
    {
        listingStandard.Label("RimTalk.Settings.LocalProviderConfiguration".Translate());
        listingStandard.Gap(6f);

        if (settings.LocalConfig == null)
        {
            settings.LocalConfig = new ApiConfig { Provider = AIProvider.Local };
        }

        DrawLocalConfigRow(listingStandard, settings.LocalConfig);
    }

    internal void DrawLocalConfigRow(Listing_Standard listingStandard, ApiConfig config)
    {
        Rect rowRect = listingStandard.GetRect(24f);
        float x = rowRect.x;
        float y = rowRect.y;
        float height = rowRect.height;

        Rect baseUrlLabelRect = new Rect(x, y, 80f, height);
        var labelText = "RimTalk.Settings.BaseUrlLabel".Translate() + " [?]";
        Widgets.Label(baseUrlLabelRect, labelText);
        TooltipHandler.TipRegion(baseUrlLabelRect, "RimTalk_Settings_Api_BaseUrlInfo".Translate());
        x += 85f;

        Rect urlRect = new Rect(x, y, 250f, height);
        config.BaseUrl = Widgets.TextField(urlRect, config.BaseUrl);
        x += 285f;

        Rect modelLabelRect = new Rect(x, y, 70f, height);
        Widgets.Label(modelLabelRect, "RimTalk.Settings.ModelLabel".Translate());
        x += 75f;

        Rect modelRect = new Rect(x, y, 200f, height);
        config.CustomModelName = Widgets.TextField(modelRect, config.CustomModelName);
    }

}
