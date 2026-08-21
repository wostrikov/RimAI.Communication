using System;
using System.Collections.Generic;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Provider/model picker semantics used by the Communication cloud settings UI.
/// Same-provider reselect must not wipe a valid SelectedModel. A model-list
/// refresh must not erase a currently set model merely because the list loaded.
/// </summary>
public static class CommunicationProviderSelection
{
    public const string Player2DefaultModel = "Default";
    public const string CustomModelSentinel = "Custom";

    public static bool ApplyProvider(CloudProviderSelectionState state, AIProvider provider)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.Provider == provider)
            return false;

        state.Provider = provider;
        switch (provider)
        {
            case AIProvider.Player2:
                state.SelectedModel = Player2DefaultModel;
                break;
            case AIProvider.Custom:
                state.SelectedModel = CustomModelSentinel;
                break;
            default:
                state.SelectedModel = CommunicationCloudSettingsPersistence.SelectedModelDefault;
                break;
        }

        return true;
    }

    public static bool PreserveSelectedModelOnRefresh(string selectedModel) =>
        !CommunicationCloudSettingsPersistence.IsUnsetModel(selectedModel);

    public static string ResolveSelectedModelAfterRefresh(string selectedModel, IReadOnlyList<string> fetchedModels)
    {
        if (PreserveSelectedModelOnRefresh(selectedModel))
            return selectedModel;

        return CommunicationCloudSettingsPersistence.SelectedModelDefault;
    }

    public static bool ModelMissingFromAuthoritativeList(string selectedModel, IReadOnlyList<string> fetchedModels)
    {
        if (fetchedModels == null || fetchedModels.Count == 0)
            return false;
        if (CommunicationCloudSettingsPersistence.IsUnsetModel(selectedModel))
            return false;
        if (string.Equals(selectedModel, CustomModelSentinel, StringComparison.Ordinal))
            return false;
        for (int i = 0; i < fetchedModels.Count; i++)
        {
            if (string.Equals(fetchedModels[i], selectedModel, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

public sealed class CloudProviderSelectionState
{
    public AIProvider Provider { get; set; }
    public string SelectedModel { get; set; } = CommunicationCloudSettingsPersistence.SelectedModelDefault;
    public string CustomModelName { get; set; } = "";
}
