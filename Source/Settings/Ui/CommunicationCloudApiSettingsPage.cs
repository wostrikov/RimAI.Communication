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
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication;

internal sealed class CommunicationCloudApiSettingsPage : CommunicationSettingsCollaborator
{
    internal CommunicationCloudApiSettingsPage(Settings owner) : base(owner) { }
    private static readonly Dictionary<string, List<string>> ModelCache = new();
    private int _modelFetchSerial;

    internal void DrawCloudProvidersSection(Listing_Standard listingStandard, CommunicationSettings settings)
    {
        Rect headerRect = listingStandard.GetRect(24f);

        // Header with add button
        float addBtnSize = 24f; 
        Rect addButtonRect = new Rect(headerRect.x + headerRect.width - addBtnSize, headerRect.y, addBtnSize, addBtnSize);
        headerRect.width -= (addBtnSize + 5f); 

        Widgets.Label(headerRect, "RimTalk.Settings.CloudApiConfigurations".Translate());

        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;
        Rect cloudDescRect = listingStandard.GetRect(Text.LineHeight * 2);
        cloudDescRect.width -= 35f;
        Widgets.Label(cloudDescRect, "RimTalk.Settings.CloudApiConfigurationsDesc".Translate());
        GUI.color = Color.white;

        // Draw Add Button (+)
        Color prevColor = GUI.color;
        GUI.color = new Color(0.3f, 0.9f, 0.3f);
        if (Widgets.ButtonText(addButtonRect, "+"))
        {
            SoundDefOf.Click.PlayOneShotOnCamera(null);
            settings.CloudConfigs.Add(new ApiConfig());
        }
        GUI.color = prevColor;
        
        listingStandard.Gap(6f);

        // --- Table Headers ---
        Rect tableHeaderRect = listingStandard.GetRect(20f);
        float x = tableHeaderRect.x;
        float y = tableHeaderRect.y;
        float height = tableHeaderRect.height;
        float totalWidth = tableHeaderRect.width;

        float providerWidth = 90f;
        float modelWidth = 190f; 
        float controlsWidth = 100f; 

        Rect providerHeaderRect = new Rect(x, y, providerWidth, height);
        Widgets.Label(providerHeaderRect, "RimTalk.Settings.ProviderHeader".Translate());
        
        float middleStartX = x + providerWidth + 5f;
        Rect apiKeyHeaderRect = new Rect(middleStartX, y, 200f, height);
        Widgets.Label(apiKeyHeaderRect, "RimTalk.Settings.ApiKeyHeader".Translate());

        Rect modelHeaderRect = new Rect(totalWidth - controlsWidth - modelWidth - 5f, y, modelWidth, height);
        Widgets.Label(modelHeaderRect, "RimTalk.Settings.ModelHeader".Translate());

        Rect enabledHeaderRect = new Rect(totalWidth - controlsWidth + 5f, y, controlsWidth, height);
        Widgets.Label(enabledHeaderRect, "RimTalk.Settings.EnabledHeader".Translate());

        listingStandard.Gap(3f);

        for (int i = 0; i < settings.CloudConfigs.Count; i++)
        {
            if (DrawCloudConfigRow(listingStandard, settings.CloudConfigs[i], i, settings.CloudConfigs))
            {
                settings.CloudConfigs.RemoveAt(i);
                i--;
            }
            listingStandard.Gap(2f);
        }

        Text.Font = GameFont.Small;
    }

    internal bool DrawCloudConfigRow(Listing_Standard listingStandard, ApiConfig config, int index, List<ApiConfig> configs)
    {
        Text.Font = GameFont.Tiny;

        Rect rowRect = listingStandard.GetRect(22f);
        float x = rowRect.x;
        float y = rowRect.y;
        float height = rowRect.height;
        float totalWidth = rowRect.width;

        float providerWidth = 90f;
        float modelWidth = 190f;
        float controlsWidth = 100f;
        float gap = 5f;

        float middleZoneWidth = totalWidth - providerWidth - modelWidth - controlsWidth - (gap * 3);
        float middleStartX = x + providerWidth + gap;

        Color originalColor = GUI.color;
        if (!config.IsEnabled)
        {
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        }

        // 1. Provider
        DrawProviderDropdown(x, y, height, providerWidth, config);
        
        // 2. Middle Zone
        if (config.Provider == AIProvider.Custom)
        {
            float keyWidth = (middleZoneWidth * 0.4f) - (gap / 2);
            float urlWidth = (middleZoneWidth * 0.6f) - (gap / 2);

            DrawApiKeyInput(middleStartX, y, height, keyWidth, config);
            DrawBaseUrlInput(middleStartX + keyWidth + gap, y, height, urlWidth, config);
        }
        else
        {
            DrawApiKeyInput(middleStartX, y, height, middleZoneWidth, config);
        }

        // 3. Model
        float modelStartX = middleStartX + middleZoneWidth + gap;
        if (config.Provider == AIProvider.Custom)
        {
            DrawCustomModelInput(modelStartX, y, height, modelWidth, config);
        }
        else
        {
            DrawDefaultModelSelector(modelStartX, y, height, modelWidth, config);
        }

        GUI.color = originalColor;

        // 4. Controls
        float btnSize = 22f;
        float btnGap = 2f;

        float deleteX = totalWidth - btnSize; 
        float downX = deleteX - btnGap - btnSize;
        float upX = downX - btnGap - btnSize;

        float controlsStartX = totalWidth - controlsWidth;
        float checkboxSpaceWidth = upX - controlsStartX;
        
        float checkboxX = controlsStartX + (checkboxSpaceWidth - 24f) / 2f;
        
        Rect toggleRect = new Rect(checkboxX, y, 24f, height);
        Widgets.Checkbox(new Vector2(toggleRect.x, toggleRect.y), ref config.IsEnabled, 20f);
        if (Mouse.IsOver(toggleRect)) TooltipHandler.TipRegion(toggleRect, "Enable/Disable");

        DrawReorderButtons(upX, y, height, index, configs);

        Rect deleteRect = new Rect(deleteX, y, btnSize, height);
        bool deleteClicked = false;
        bool canDelete = configs.Count > 1;

        Color prevColor = GUI.color;
        if (canDelete)
        {
            GUI.color = new Color(1f, 0.4f, 0.4f);
        }
        else
        {
            GUI.color = Color.gray;
        }

        if (Widgets.ButtonText(deleteRect, "×", active: canDelete))
        {
            SoundDefOf.Click.PlayOneShotOnCamera(null);
            deleteClicked = true;
        }
        GUI.color = prevColor;

        Text.Font = GameFont.Tiny;
        return deleteClicked;
    }

    internal void DrawReorderButtons(float x, float y, float height, int index, List<ApiConfig> configs)
    {
        float btnSize = 22f;
        Rect upButtonRect = new Rect(x, y, btnSize, height);
        
        if (Widgets.ButtonText(upButtonRect, "▲") && index > 0)
        {
            SoundDefOf.Click.PlayOneShotOnCamera(null);
            (configs[index], configs[index - 1]) = (configs[index - 1], configs[index]);
        }

        Rect downButtonRect = new Rect(x + btnSize + 2f, y, btnSize, height);

        if (Widgets.ButtonText(downButtonRect, "▼") && index < configs.Count - 1)
        {
            SoundDefOf.Click.PlayOneShotOnCamera(null);
            (configs[index], configs[index + 1]) = (configs[index + 1], configs[index]);
        }
    }

    internal void DrawDefaultModelSelector(float x, float y, float height, float width, ApiConfig config)
    {
        Rect modelRect = new Rect(x, y, width, height);
        if (config.SelectedModel == "Custom")
        {
            float xButtonWidth = 22f;
            float textFieldWidth = width - xButtonWidth - 2f;

            Rect textFieldRect = new Rect(x, y, textFieldWidth, height);
            Rect backButtonRect = new Rect(x + textFieldWidth + 2f, y, xButtonWidth, height);

            config.CustomModelName = DrawTextFieldWithPlaceholder(textFieldRect, config.CustomModelName, "Model ID");
            
            if (Widgets.ButtonText(backButtonRect, "×"))
            {
                SoundDefOf.Click.PlayOneShotOnCamera(null);
                config.SelectedModel = Constant.ChooseModel;
            }
        }
        else
        {
            if (Widgets.ButtonText(modelRect, config.SelectedModel))
            {
                try
                {
                    ShowModelSelectionMenu(config);
                }
                catch (Exception ex)
                {
                    RimAiLog.Error(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker click failed.\n" + ex);
                }
            }
        }
    }

    internal string DrawTextFieldWithPlaceholder(Rect rect, string text, string placeholder)
    {
        string result = Widgets.TextField(rect, text);
        
        if (string.IsNullOrEmpty(result))
        {
            TextAnchor originalAnchor = Text.Anchor;
            Color originalColor = GUI.color;

            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.7f); 
            
            Rect labelRect = new Rect(rect.x + 5f, rect.y, rect.width - 5f, rect.height);
            Widgets.Label(labelRect, placeholder);

            GUI.color = originalColor;
            Text.Anchor = originalAnchor;
        }

        return result;
    }

    internal void DrawProviderDropdown(float x, float y, float height, float width, ApiConfig config)
    {
        Rect providerRect = new Rect(x, y, width, height);
        if (Widgets.ButtonText(providerRect, config.Provider.GetLabel()))
        {
            List<FloatMenuOption> providerOptions = [];
            foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
            {
                if (provider is AIProvider.None or AIProvider.Local) continue;
                
                providerOptions.Add(new FloatMenuOption(provider.GetLabel(), () =>
                {
                    config.Provider = provider;
                    switch (provider)
                    {
                        case AIProvider.Player2:
                            config.SelectedModel = "Default";
                            Player2Client.CheckPlayer2StatusAndNotify();
                            break;
                        case AIProvider.Custom:
                            config.SelectedModel = "Custom";
                            break;
                        default:
                            config.SelectedModel = Constant.ChooseModel;
                            break;
                    }
                }));
            }
            Find.WindowStack.Add(new FloatMenu(providerOptions));
        }
    }

    internal void DrawApiKeyInput(float x, float y, float height, float width, ApiConfig config)
    {
        Rect apiKeyRect = new Rect(x, y, width, height);
        if (config.Provider == AIProvider.OpenAI)
        {
            Widgets.Label(apiKeyRect, OpenAIProviderAdapter.CredentialDisplay);
            return;
        }
        config.ApiKey = DrawTextFieldWithPlaceholder(apiKeyRect, config.ApiKey, "Paste API Key...");
    }

    internal void DrawBaseUrlInput(float x, float y, float height, float width, ApiConfig config)
    {
        Rect baseUrlRect = new Rect(x, y, width, height);
        config.BaseUrl = DrawTextFieldWithPlaceholder(baseUrlRect, config.BaseUrl, "https://...");
        if (Mouse.IsOver(baseUrlRect)) TooltipHandler.TipRegion(baseUrlRect, "RimTalk_Settings_Api_BaseUrlInfo".Translate());
    }

    internal void DrawCustomModelInput(float x, float y, float height, float width, ApiConfig config)
    {
        Rect customModelRect = new Rect(x, y, width, height);
        config.CustomModelName = DrawTextFieldWithPlaceholder(customModelRect, config.CustomModelName, "Model ID");
        config.SelectedModel = string.IsNullOrWhiteSpace(config.CustomModelName)
            ? Constant.ChooseModel
            : config.CustomModelName;
    }

    internal void ShowModelSelectionMenu(ApiConfig config)
    {
        var provider = config.Provider;
        RimAiLog.Info(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker opened. provider=" + provider
            + " selected=" + config.SelectedModel
            + " scheduler=" + (MainThreadSchedulerAccess.IsAvailable ? "available" : "missing")
            + " syncContext=" + (System.Threading.SynchronizationContext.Current?.GetType().FullName ?? "null"));

        // Allow Player2 to work without API key (local app detection)
        if ((provider == AIProvider.OpenAI ? !OpenAIProviderAdapter.CredentialPresent : string.IsNullOrWhiteSpace(config.ApiKey))
            && provider != AIProvider.Player2)
        {
            RimAiLog.Warning(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker blocked: API credential is missing. provider=" + provider);
            Find.WindowStack.Add(new FloatMenu([new FloatMenuOption("RimTalk.Settings.EnterApiKey".Translate(), null)]));
            return;
        }

        if (provider == AIProvider.Player2)
        {
            config.SelectedModel = "Default";
            return;
        }

        string url = provider.GetListModelsUrl();

        void OpenMenu(List<string> models)
        {
            if (Find.WindowStack == null)
            {
                RimAiLog.Error(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker cannot open a menu: WindowStack is null.");
                return;
            }

            var options = new List<FloatMenuOption>();

            if (models != null && models.Any())
            {
                options.AddRange(models.Select(model => new FloatMenuOption(model, () => config.SelectedModel = model)));
            }
            else
            {
                RimAiLog.Warning(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker has no models. provider=" + provider + " url=" + url);
                options.Add(new FloatMenuOption("Ustas.RimAI.Settings.NoModelsFound".Translate(), null));
            }

            options.Add(new FloatMenuOption("Custom", () => config.SelectedModel = "Custom"));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (string.IsNullOrEmpty(url))
        {
            RimAiLog.Warning(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker has no list-models URL. provider=" + provider
                + "; opening Custom-only menu.");
            OpenMenu(null);
            return;
        }

        if (ModelCache.TryGetValue(url, out var cached))
        {
            OpenMenu(cached);
            return;
        }

        int fetchId = ++_modelFetchSerial;
        Find.WindowStack.Add(new FloatMenu(
        [
            new FloatMenuOption("Custom", () => config.SelectedModel = "Custom"),
            new FloatMenuOption("Ustas.RimAI.Settings.FetchingModels".Translate(), null)
        ]));

        string credential = provider == AIProvider.OpenAI ? OpenAIProviderAdapter.ResolveCredential() : config.ApiKey;
        RimAiLog.Info(RimAiLogCategory.Communication, "[RimAI.Communication] Fetching models. provider=" + provider + " url=" + url);
        OpenAIClient.FetchModelsAsync(credential, url).ContinueWith(task =>
        {
            try
            {
                List<string> models = null;
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    models = task.Result;
                    RimAiLog.Info(RimAiLogCategory.Communication, "[RimAI.Communication] Model fetch completed. count="
                        + (models?.Count ?? 0) + " url=" + url);
                }
                else
                {
                    RimAiLog.Error(RimAiLogCategory.Communication, "[RimAI.Communication] Model fetch failed. status=" + task.Status
                        + " url=" + url + "\n" + (task.Exception?.ToString() ?? "no exception"));
                }

                MainThreadSchedulerAccess.RunInlineOrEnqueue(() =>
                {
                    try
                    {
                        if (fetchId != _modelFetchSerial)
                        {
                            RimAiLog.Warning(RimAiLogCategory.Communication, "[RimAI.Communication] Ignoring stale model fetch " + fetchId
                                + "; current=" + _modelFetchSerial);
                            return;
                        }
                        if (models != null && models.Count > 0)
                            ModelCache[url] = models;
                        if (config.SelectedModel != Constant.ChooseModel)
                        {
                            RimAiLog.Info(RimAiLogCategory.Communication, "[RimAI.Communication] Model already chosen (" + config.SelectedModel
                                + "); not replacing the menu.");
                            return;
                        }
                        Find.WindowStack?.TryRemove(typeof(FloatMenu));
                        OpenMenu(models);
                    }
                    catch (Exception ex)
                    {
                        RimAiLog.Error(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker failed to open the result menu.\n" + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Communication, "[RimAI.Communication] Model picker continuation failed.\n" + ex);
            }
        });
    }

    internal void DrawEnableToggle(Rect rowRect, float y, float height, ApiConfig config)
    {
        Rect toggleRect = new Rect(rowRect.xMax - 70f, y, 24f, height);
        Widgets.Checkbox(new Vector2(toggleRect.x, toggleRect.y), ref config.IsEnabled);
        if (Mouse.IsOver(toggleRect))
        {
            TooltipHandler.TipRegion(toggleRect, "RimTalk.Settings.EnableDisableApiConfigTooltip".Translate());
        }
    }

}
