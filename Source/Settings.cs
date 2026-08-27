using System;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Core.Communication;
using Ustas.RimAI.Core.Configuration;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Communication;

public class Settings : Mod
{
    public const string Version = "1.0.16";

    internal CommunicationSettingsPages Pages;

    internal Vector2 _mainScrollPosition = Vector2.zero;
    internal Vector2 _aiInstructionScrollPos = Vector2.zero;
    internal Vector2 _promptContentScrollPos = Vector2.zero;
    internal string _textAreaBuffer = "";
    internal bool _textAreaInitialized;
    internal string _aiInstructionPresetId = "";
    internal int _lastTextAreaCursorPos = -1;
    internal int _lastPromptEditorCursorPos = -1;
    internal int _apiSettingsHash = 0;

    internal Vector2 _presetListScrollPos = Vector2.zero;
    internal Vector2 _entryListScrollPos = Vector2.zero;
    internal Vector2 _auxScrollPos = Vector2.zero;
    internal Vector2 _previewScrollPos = Vector2.zero;
    internal string _selectedPresetId;
    internal string _selectedEntryId;
    internal bool _showPreview = false;
    internal bool _showSidePanel = false;
    internal int _sidePanelMode = 0;
    internal float _splitRatioVert = 0.5f;
    internal float _splitRatioHoriz = 0.7f;
    internal bool _isDraggingVert = false;
    internal bool _isDraggingHoriz = false;
    internal string _variableSearchQuery = "";
    internal string _depthBuffer = "";
    internal string _depthBufferEntryId = "";

    // Tab system
    private enum SettingsTab
    {
        Basic,
        PromptPreset,
        Context,
        EventFilter
    }
    public enum ButtonDisplayMode
    {
        Tab,
        Toggle,
        None
    }
    public enum PlayerDialogueMode
    {
        Disabled,
        Manual,
        AIDrivenPawnOnly,
        AIDriven
    }

    private SettingsTab _currentTab = SettingsTab.Basic;

    private static CommunicationSettings _settings;
    static bool _hostedCommitInProgress;

    public static CommunicationSettings Get()
    {
        return _settings ??= LoadedModManager.GetMod<Settings>().GetSettings<CommunicationSettings>();
    }

    public Settings(ModContentPack content) : base(content)
    {
        Pages = new CommunicationSettingsPages(this);
        var settings = GetSettings<CommunicationSettings>();
        _apiSettingsHash = GetApiSettingsHash(settings);
        CommunicationComposition.Current.Bind(this);
        RimAiHandshake.TryActivate(
            RimAiHandshakeDescriptor.Current(RimAiModuleIds.Communication, Version, isOptional: true),
            CommunicationComposition.Current.Start);
        LongEventHandler.ExecuteWhenFinished(RimAICommunicationPersistenceLiveProbe.TryRun);
    }

    internal void RegisterRimAIContributions()
    {
        CommunicationApplicationAccess.Register(new CommunicationApplication());
        PromptTemplateAccess.Register(new CommunicationPromptTemplateRenderer());
        PromptVariableHostAccess.Register(new CommunicationPromptVariableHost());
        RimAIModuleRegistry.Current.Register(new RimAIModuleDescriptor(
            "communication",
            "RimAI.Communication",
            "RimAI.Communication",
            "Communication"));
        SharedTextAiAccess.Register(ResolveSharedTextSnapshot);
        RimAISettingsContributionRegistry.Current.Register(new DelegateSettingsContributor(
            "communication-ai",
            "AI",
            RimAISettingsSection.SharedAi,
            0,
            listing => Pages.Api.DrawSharedAiSettings((Listing_Standard)listing),
            "communication",
            "ai",
            CommitHostedSettings));
        RimAISettingsContributionRegistry.Current.Register(new DelegateSettingsContributor(
            "communication",
            "Communication",
            RimAISettingsSection.Module,
            10,
            listing => Pages.Basic.DrawBasicSettings((Listing_Standard)listing),
            "communication",
            "general",
            CommitHostedSettings));
    }

    internal static void CommitHostedSettings()
    {
        if (_hostedCommitInProgress)
            return;
        _hostedCommitInProgress = true;
        try
        {
            LoadedModManager.GetMod<Settings>()?.WriteSettings();
        }
        finally
        {
            _hostedCommitInProgress = false;
        }
    }

    static SharedTextAiSnapshot ResolveSharedTextSnapshot()
    {
        var settings = Get();
        var config = settings?.GetActiveConfig();
        if (config == null)
        {
            var inactive = SharedTextAiSnapshot.Inactive(settings?.UseCloudProviders ?? true);
            inactive.Language = GameplayAiLanguage.Resolve(settings?.GameplayAiLanguage, TryActiveGameLanguageEnglish());
            return inactive;
        }

        return SharedTextAiSnapshot.FromSelection(
            hasActive: true,
            useCloud: settings.UseCloudProviders,
            providerId: config.Provider.ToString(),
            model: config.SelectedModel,
            customModel: config.CustomModelName,
            baseUrl: config.BaseUrl,
            apiKey: config.ApiKey,
            language: settings.GameplayAiLanguage,
            gameLanguageFallback: TryActiveGameLanguageEnglish());
    }

    internal static string TryActiveGameLanguageEnglish()
    {
        try
        {
            var language = LanguageDatabase.activeLanguage;
            if (language?.info == null)
                return language?.folderName;
            if (!string.IsNullOrWhiteSpace(language.info.friendlyNameEnglish))
                return language.info.friendlyNameEnglish;
            return language.folderName;
        }
        // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — boot-time language lookup must not fail settings
        catch (Exception ex)
        {
            RimAiLog.Warning(RimAiLogCategory.Communication, "[RimAI.Communication] TryActiveGameLanguageEnglish failed: " + ex);
            return null;
        }
    }

    public override string SettingsCategory() =>
        Content?.Name ?? "RimAI.Communication";

    public override void WriteSettings()
    {
        base.WriteSettings();
        ClearCache();
        CommunicationSettings settings = Get();
        int newHash = GetApiSettingsHash(settings);

        if (newHash != _apiSettingsHash)
        {
            _apiSettingsHash = newHash;
            RimTalk.Reset(true);
        }

        RimAiLog.Info(
            RimAiLogCategory.Communication,
            "[RimAI.Communication] settings committed configs=" + (settings.CloudConfigs?.Count ?? 0)
            + " index=" + settings.CurrentCloudConfigIndex
            + " useSimpleConfig=" + settings.UseSimpleConfig
            + " useCloudProviders=" + settings.UseCloudProviders
            + " selected=" + DescribeSelectedModel(settings));
    }

    static string DescribeSelectedModel(CommunicationSettings settings)
    {
        if (settings?.CloudConfigs == null || settings.CloudConfigs.Count == 0)
            return CommunicationCloudSettingsPersistence.SelectedModelDefault;
        int index = CommunicationCloudSettingsPersistence.NormalizeIndex(
            settings.CurrentCloudConfigIndex,
            settings.CloudConfigs.Count);
        return settings.CloudConfigs[index]?.SelectedModel
            ?? CommunicationCloudSettingsPersistence.SelectedModelDefault;
    }

    private int GetApiSettingsHash(CommunicationSettings settings)
    {
        var sb = new StringBuilder();
            
        if (settings.CloudConfigs != null)
        {
            foreach (var config in settings.CloudConfigs)
            {
                sb.AppendLine(config.Provider.ToString());
                sb.AppendLine(config.ApiKey);
                sb.AppendLine(config.SelectedModel);
                sb.AppendLine(config.CustomModelName);
                sb.AppendLine(config.IsEnabled.ToString());
                sb.AppendLine(config.BaseUrl);
            }
        }
        sb.AppendLine(settings.CurrentCloudConfigIndex.ToString());
        sb.AppendLine(settings.UseSimpleConfig.ToString());
        sb.AppendLine(settings.UseCloudProviders.ToString());
        if (settings.LocalConfig != null)
        {
            sb.AppendLine(settings.LocalConfig.Provider.ToString());
            sb.AppendLine(settings.LocalConfig.BaseUrl);
            sb.AppendLine(settings.LocalConfig.CustomModelName);
        }

        sb.AppendLine(settings.AllowSimultaneousConversations.ToString());
        sb.AppendLine(settings.AllowSlavesToTalk.ToString());
        sb.AppendLine(settings.AllowPrisonersToTalk.ToString());
        sb.AppendLine(settings.AllowOtherFactionsToTalk.ToString());
        sb.AppendLine(settings.AllowEnemiesToTalk.ToString());
        sb.AppendLine(settings.AllowBabiesToTalk.ToString());
        sb.AppendLine(settings.AllowChildrenToTalk.ToString());
        sb.AppendLine(settings.AllowNonHumanToTalk.ToString());
        sb.AppendLine(settings.ApplyMoodAndSocialEffects.ToString());
        sb.AppendLine(settings.PlayerDialogueMode.ToString());
        sb.AppendLine(settings.PlayerName);
        
        return sb.ToString().GetHashCode();
    }

    private void DrawTabButtons(Rect rect)
    {
        float tabWidth = rect.width / 4f;

        Rect basicTabRect = new Rect(rect.x, rect.y, tabWidth, 30f);
        Rect promptTabRect = new Rect(rect.x + tabWidth, rect.y, tabWidth, 30f);
        Rect contextTabRect = new Rect(rect.x + tabWidth * 2, rect.y, tabWidth, 30f);
        Rect filterTabRect = new Rect(rect.x + tabWidth * 3, rect.y, tabWidth, 30f);

        GUI.color = _currentTab == SettingsTab.Basic ? Color.white : Color.gray;
        if (Widgets.ButtonText(basicTabRect, "RimTalk.Settings.BasicSettings".Translate()))
        {
            _currentTab = SettingsTab.Basic;
        }

        GUI.color = _currentTab == SettingsTab.PromptPreset ? Color.white : Color.gray;
        if (Widgets.ButtonText(promptTabRect, "RimTalk.Settings.PromptSetting".Translate()))
        {
            _currentTab = SettingsTab.PromptPreset;
        }

        GUI.color = _currentTab == SettingsTab.Context ? Color.white : Color.gray;
        if (Widgets.ButtonText(contextTabRect, "RimTalk.Settings.ContextFilter".Translate()))
        {
            _currentTab = SettingsTab.Context;
        }

        GUI.color = _currentTab == SettingsTab.EventFilter ? Color.white : Color.gray;
        if (Widgets.ButtonText(filterTabRect, "RimTalk.Settings.EventFilter".Translate()))
        {
            _currentTab = SettingsTab.EventFilter;
            if (!Pages.EventFilter.ArchivableTypesScanned)
            {
                Pages.EventFilter.ScanForArchivableTypes();
            }
        }

        GUI.color = Color.white;
    }
        
    public override void DoSettingsWindowContents(Rect inRect)
    {
        RimAISettingsNavigation.Open("communication", "general");
        CommunicationSettings rtSettings = Get();
        
        // Settings window hacks
        var settingsWindow = Find.WindowStack.WindowOfType<Dialog_ModSettings>();
        if (settingsWindow != null)
        {
            settingsWindow.doCloseX = true;
            settingsWindow.draggable = true;
            settingsWindow.closeOnAccept = false;
            settingsWindow.absorbInputAroundWindow = false;
            settingsWindow.preventCameraMotion = false;
            settingsWindow.closeOnClickedOutside = false;

            // Dynamically resize if in Advanced Prompt mode, otherwise reset to standard size
            float targetWidth;
            float targetHeight;

            if (_currentTab == SettingsTab.PromptPreset && rtSettings.UseAdvancedPromptMode)
            {
                targetWidth = Mathf.Min(Verse.UI.screenWidth * 0.9f, 1200f);
                targetHeight = Mathf.Min(Verse.UI.screenHeight * 0.9f, 800f);
            }
            else
            {
                targetWidth = 900f;
                targetHeight = 700f;
            }

            if (Mathf.Abs(settingsWindow.windowRect.width - targetWidth) > 1f || 
                Mathf.Abs(settingsWindow.windowRect.height - targetHeight) > 1f)
            {
                settingsWindow.windowRect.width = targetWidth;
                settingsWindow.windowRect.height = targetHeight;
                settingsWindow.windowRect.x = (Verse.UI.screenWidth - targetWidth) / 2f;
                settingsWindow.windowRect.y = (Verse.UI.screenHeight - targetHeight) / 2f;
            }
        }
        
        // 1. Draw Tabs
        Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
        DrawTabButtons(tabRect);

        // 2. Define Content Area
        Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);

        // 3. Special Case: Prompt Preset Tab (Advanced Mode only)
        // Why this is different: Advanced Mode contains a complex, full-height editor 
        // that handles its own internal scrolling. Wrapping it in a main ScrollView causes 
        // nested scroll issues.
        if (_currentTab == SettingsTab.PromptPreset && rtSettings.UseAdvancedPromptMode)
        {
            Listing_Standard promptListing = new Listing_Standard();
            // Verse wraps a Listing into a second column, off the visible view, as soon as
            // content passes the rect height, and CurHeight then reports that new column.
            // A scrolling settings page never wants that; see validate_scrollable_listings.
            promptListing.maxOneColumn = true;
            promptListing.Begin(contentRect);
            Pages.PromptPreset.DrawPromptPresetSettings(promptListing, contentRect);
            promptListing.End();
            return;
        }

        // 4. Standard Logic for other tabs (Scrollable Lists)
        // --- Off-screen height calculation ---
        GUI.BeginGroup(new Rect(-9999, -9999, 1, 1)); 
        Listing_Standard listing = new Listing_Standard();
        Rect calculationRect = new Rect(0, 0, contentRect.width - 16f, 9999f);
        listing.Begin(calculationRect);

        switch (_currentTab)
        {
            case SettingsTab.Basic:
                Pages.Basic.DrawBasicSettings(listing);
                break;
            case SettingsTab.PromptPreset:
                Pages.PromptPreset.DrawPromptPresetSettings(listing, contentRect);
                break;
            case SettingsTab.Context:
                Pages.ContextFilter.DrawContextFilterSettings(listing);
                break;
            case SettingsTab.EventFilter:
                Pages.EventFilter.DrawEventFilterSettings(listing);
                break;
        }

        float contentHeight = listing.CurHeight;
        listing.End();
        GUI.EndGroup();

        // --- Real Draw ---
        Rect viewRect = new Rect(0f, 0f, contentRect.width - 16f, contentHeight);
        _mainScrollPosition = GUI.BeginScrollView(contentRect, _mainScrollPosition, viewRect);

        // The measured height is exact, so a wrap should be impossible here - but
        // it costs one line to make that a fact rather than an argument.
        listing.maxOneColumn = true;
        listing.Begin(viewRect);

        switch (_currentTab)
        {
            case SettingsTab.Basic:
                Pages.Basic.DrawBasicSettings(listing);
                break;
            case SettingsTab.PromptPreset:
                Pages.PromptPreset.DrawPromptPresetSettings(listing, contentRect);
                break;
            case SettingsTab.Context:
                Pages.ContextFilter.DrawContextFilterSettings(listing);
                break;
            case SettingsTab.EventFilter:
                Pages.EventFilter.DrawEventFilterSettings(listing);
                break;
        }

        listing.End();
        GUI.EndScrollView();
    }
    
    private static void ClearCache()
    {
        _settings = null;
    }

    public void DrawPromptPresetSettings(Listing_Standard listingStandard, Rect inRect)
    {
        Pages.PromptPreset.DrawPromptPresetSettings(listingStandard, inRect);
    }
}
