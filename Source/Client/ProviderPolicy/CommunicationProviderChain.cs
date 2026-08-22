using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Client.ProviderPolicy;

public sealed class CommunicationProviderSlot
{
    public string SlotId { get; set; }
    public AIProvider Provider { get; set; }
    public string SelectedModel { get; set; }
    public string CustomModelName { get; set; }
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public bool OpenAiUsesSharedCredential { get; set; }

    public string DiagnosticName =>
        Provider + ":" + (string.IsNullOrWhiteSpace(SelectedModel) ? CustomModelName : SelectedModel);
}

public sealed class CommunicationProviderChainRequest
{
    public CommunicationCloudSettingsSnapshot Settings { get; set; }
    public string SimpleApiKey { get; set; }
    public string SimpleDefaultModel { get; set; }
    public string SimpleFallbackModel { get; set; }
    public bool OpenAiCredentialPresent { get; set; }
    public bool AllowLocalFailover { get; set; } = true;
}

/// <summary>
/// Single source of truth for Communication provider order. Only enabled and
/// configured slots are included. Duplicates are dropped. Local is appended
/// after cloud configs when failover is allowed and the local endpoint is valid.
/// </summary>
public static class CommunicationProviderChain
{
    public static IReadOnlyList<CommunicationProviderSlot> Build(CommunicationProviderChainRequest request)
    {
        var slots = new List<CommunicationProviderSlot>();
        if (request == null)
            return slots;

        var settings = request.Settings ?? new CommunicationCloudSettingsSnapshot();
        if (settings.UseSimpleConfig)
        {
            if (string.IsNullOrWhiteSpace(request.SimpleApiKey))
                return slots;
            AddIfNew(slots, SimpleSlot("simple:default", request.SimpleDefaultModel, request.SimpleApiKey));
            if (!string.IsNullOrWhiteSpace(request.SimpleFallbackModel) &&
                !string.Equals(request.SimpleFallbackModel, request.SimpleDefaultModel, StringComparison.Ordinal))
            {
                AddIfNew(slots, SimpleSlot("simple:fallback", request.SimpleFallbackModel, request.SimpleApiKey));
            }

            return slots;
        }

        if (!settings.UseCloudProviders)
        {
            TryAddRecord(slots, "local", settings.LocalConfig, request, useCloudProviders: false);
            return slots;
        }

        var configs = settings.CloudConfigs ?? new List<CloudConfigRecord>();
        var start = CommunicationCloudSettingsPersistence.NormalizeIndex(settings.CurrentCloudConfigIndex, configs.Count);
        for (var i = 0; i < configs.Count; i++)
        {
            var index = (start + i) % configs.Count;
            TryAddRecord(slots, "cloud:" + index, configs[index], request, useCloudProviders: true);
        }

        if (request.AllowLocalFailover)
            TryAddRecord(slots, "local", settings.LocalConfig, request, useCloudProviders: false);

        return slots;
    }

    static CommunicationProviderSlot SimpleSlot(string id, string model, string apiKey) =>
        new CommunicationProviderSlot
        {
            SlotId = id,
            Provider = AIProvider.Google,
            SelectedModel = model ?? string.Empty,
            CustomModelName = string.Empty,
            BaseUrl = string.Empty,
            ApiKey = apiKey ?? string.Empty
        };

    static void TryAddRecord(
        List<CommunicationProviderSlot> slots,
        string slotId,
        CloudConfigRecord record,
        CommunicationProviderChainRequest request,
        bool useCloudProviders)
    {
        if (record == null)
            return;
        if (!CommunicationCloudSettingsPersistence.IsCloudConfigValid(
                record.IsEnabled,
                record.Provider,
                record.SelectedModel,
                record.ApiKey,
                record.BaseUrl,
                useCloudProviders,
                request.OpenAiCredentialPresent))
        {
            return;
        }

        AddIfNew(slots, new CommunicationProviderSlot
        {
            SlotId = slotId,
            Provider = record.Provider,
            SelectedModel = record.SelectedModel ?? string.Empty,
            CustomModelName = record.CustomModelName ?? string.Empty,
            BaseUrl = record.BaseUrl ?? string.Empty,
            ApiKey = record.ApiKey ?? string.Empty,
            OpenAiUsesSharedCredential = record.Provider == AIProvider.OpenAI
        });
    }

    static void AddIfNew(List<CommunicationProviderSlot> slots, CommunicationProviderSlot slot)
    {
        if (slot == null)
            return;
        foreach (var existing in slots)
        {
            if (SameIdentity(existing, slot))
                return;
        }

        slots.Add(slot);
    }

    static bool SameIdentity(CommunicationProviderSlot left, CommunicationProviderSlot right) =>
        left.Provider == right.Provider &&
        string.Equals(left.SelectedModel, right.SelectedModel, StringComparison.Ordinal) &&
        string.Equals(left.CustomModelName, right.CustomModelName, StringComparison.Ordinal) &&
        string.Equals(left.BaseUrl, right.BaseUrl, StringComparison.Ordinal);
}
