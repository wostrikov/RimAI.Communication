using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Verse-free Communication cloud-configuration persistence semantics.
/// SelectedModel default is the explicit unset sentinel, not an unrelated
/// provider model. CurrentCloudConfigIndex is durable. IsUsingFallbackModel
/// is transient retry state and is not persisted.
/// </summary>
public static class CommunicationCloudSettingsPersistence
{
    public const string SelectedModelDefault = "(choose model)";
    public const string SelectedModelScribeKey = "selectedModel";
    public const string CurrentCloudConfigIndexScribeKey = "currentCloudConfigIndex";
    public const bool PersistIsUsingFallbackModel = false;

    public static int NormalizeIndex(int index, int count)
    {
        if (count <= 0)
            return 0;
        if (index < 0 || index >= count)
            return 0;
        return index;
    }

    public static CommunicationCloudSettingsSnapshot Roundtrip(CommunicationCloudSettingsSnapshot source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var loaded = Clone(source);
        if (loaded.CloudConfigs == null)
            loaded.CloudConfigs = new List<CloudConfigRecord>();
        if (loaded.CloudConfigs.Count == 0)
            loaded.CloudConfigs.Add(new CloudConfigRecord());
        if (loaded.LocalConfig == null)
            loaded.LocalConfig = new CloudConfigRecord { Provider = AIProvider.Local };

        foreach (var config in loaded.CloudConfigs)
            NormalizeLoadedConfig(config);
        NormalizeLoadedConfig(loaded.LocalConfig);
        loaded.CurrentCloudConfigIndex = NormalizeIndex(loaded.CurrentCloudConfigIndex, loaded.CloudConfigs.Count);
        loaded.IsUsingFallbackModel = false;
        return loaded;
    }

    public static bool IsCloudConfigValid(
        bool isEnabled,
        AIProvider provider,
        string selectedModel,
        string apiKey,
        string baseUrl,
        bool useCloudProviders,
        bool openAiCredentialPresent)
    {
        if (!isEnabled)
            return false;

        if (!useCloudProviders)
            return !string.IsNullOrWhiteSpace(baseUrl);

        if (provider == AIProvider.Player2)
            return !IsUnsetModel(selectedModel);

        bool hasCredential = provider == AIProvider.OpenAI
            ? openAiCredentialPresent
            : !string.IsNullOrWhiteSpace(apiKey);
        return hasCredential && !IsUnsetModel(selectedModel);
    }

    public static bool IsUnsetModel(string selectedModel) =>
        string.IsNullOrWhiteSpace(selectedModel) ||
        string.Equals(selectedModel, SelectedModelDefault, StringComparison.Ordinal);

    static void NormalizeLoadedConfig(CloudConfigRecord config)
    {
        if (config == null)
            return;
        if (config.SelectedModel == null)
            config.SelectedModel = SelectedModelDefault;
        if (config.CustomModelName == null)
            config.CustomModelName = "";
        if (config.BaseUrl == null)
            config.BaseUrl = "";
        if (config.Provider == AIProvider.OpenAI)
            config.ApiKey = "";
        else if (config.ApiKey == null)
            config.ApiKey = "";
    }

    static CommunicationCloudSettingsSnapshot Clone(CommunicationCloudSettingsSnapshot source)
    {
        var loaded = new CommunicationCloudSettingsSnapshot
        {
            CurrentCloudConfigIndex = source.CurrentCloudConfigIndex,
            UseSimpleConfig = source.UseSimpleConfig,
            UseCloudProviders = source.UseCloudProviders,
            LocalConfig = CloneConfig(source.LocalConfig)
        };
        if (source.CloudConfigs != null)
        {
            foreach (var config in source.CloudConfigs)
                loaded.CloudConfigs.Add(CloneConfig(config));
        }
        return loaded;
    }

    static CloudConfigRecord CloneConfig(CloudConfigRecord source)
    {
        if (source == null)
            return null;
        return new CloudConfigRecord
        {
            IsEnabled = source.IsEnabled,
            Provider = source.Provider,
            SelectedModel = source.SelectedModel,
            CustomModelName = source.CustomModelName,
            BaseUrl = source.BaseUrl,
            ApiKey = source.ApiKey
        };
    }
}

public sealed class CommunicationCloudSettingsSnapshot
{
    public List<CloudConfigRecord> CloudConfigs { get; set; } = new();
    public int CurrentCloudConfigIndex { get; set; }
    public bool UseSimpleConfig { get; set; }
    public bool UseCloudProviders { get; set; } = true;
    public CloudConfigRecord LocalConfig { get; set; }
    public bool IsUsingFallbackModel { get; set; }
}

public sealed class CloudConfigRecord
{
    public bool IsEnabled { get; set; } = true;
    public AIProvider Provider { get; set; } = AIProvider.Google;
    public string SelectedModel { get; set; } = CommunicationCloudSettingsPersistence.SelectedModelDefault;
    public string CustomModelName { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
}
