using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Client.OpenAI;
using Ustas.RimAI.Communication.Client.Player2;
using Ustas.RimAI.Communication.Client.ProviderPolicy;
using Ustas.RimAI.Core.Configuration;

namespace Ustas.RimAI.Communication.Client;

/// <summary>
/// Factory for creating AI client instances with support for async initialization
/// Handles Player2 local app detection and fallback mechanisms
/// </summary>
public static class AIClientFactory
{
    private static IAIClient _instance;
    private static AIProvider _currentProvider;

    /// <summary>
    /// Async method for getting AI client - required for Player2 local detection
    /// </summary>
    public static async Task<IAIClient> GetAIClientAsync()
    {
        var config = Settings.Get().GetActiveConfig();
        if (config == null && SharedTextAiAccess.Current is not { HasActive: true })
        {
            return null;
        }
        if (config == null)
        {
            return null;
        }

        if (_instance == null || _currentProvider != config.Provider)
        {
            _instance = await CreateClientAsync(config);
            _currentProvider = config.Provider;
        }

        return _instance;
    }

    public static ApiConfig ToApiConfig(CommunicationProviderSlot slot)
    {
        if (slot == null)
            return null;
        return new ApiConfig
        {
            IsEnabled = true,
            Provider = slot.Provider,
            ApiKey = slot.ApiKey ?? string.Empty,
            SelectedModel = slot.SelectedModel ?? string.Empty,
            CustomModelName = slot.CustomModelName ?? string.Empty,
            BaseUrl = slot.BaseUrl ?? string.Empty
        };
    }

    /// <summary>
    /// Creates a client for one ephemeral provider slot without mutating the
    /// cached singleton used by <see cref="GetAIClientAsync"/>.
    /// </summary>
    public static Task<IAIClient> CreateClientAsync(CommunicationProviderSlot slot) =>
        CreateClientAsync(ToApiConfig(slot));

    /// <summary>
    /// Creates appropriate AI client instance based on provider configuration
    /// Player2 uses async factory method for local app detection
    /// </summary>
    public static async Task<IAIClient> CreateClientAsync(ApiConfig config)
    {
        if (config == null)
            return null;
        var model = config.SelectedModel == "Custom" ? config.CustomModelName : config.SelectedModel;

        // 1. Handle Special/Dynamic cases
        switch (config.Provider)
        {
            case AIProvider.OpenAI:
                return new OpenAIClient(OpenAIProviderAdapter.ResponsesEndpoint, model,
                    OpenAIProviderAdapter.ResolveCredential(), officialOpenAI: true);
            case AIProvider.Player2: return await Player2Client.CreateAsync(config.ApiKey);
            case AIProvider.Local:   return new OpenAIClient(config.BaseUrl, config.CustomModelName);
            case AIProvider.Custom:  return new OpenAIClient(config.BaseUrl, config.CustomModelName, config.ApiKey);
        }

        var endpoint = GameplayTextAiProviderCatalog.ChatEndpoint(config.Provider.ToString());
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return new OpenAIClient(
                endpoint,
                model,
                config.ApiKey,
                ToMutableHeaders(GameplayTextAiProviderCatalog.ExtraHeaders(config.Provider.ToString())));
        }

        return null;
    }

    static Dictionary<string, string> ToMutableHeaders(IReadOnlyDictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
            return null;
        var copy = new Dictionary<string, string>();
        foreach (var pair in headers)
            copy[pair.Key] = pair.Value;
        return copy;
    }

    /// <summary>
    /// Clean up resources and stop background processes
    /// </summary>
    public static void Clear()
    {
        if (_currentProvider == AIProvider.Player2)
        {
            Player2Client.StopHealthCheck();
        }
        _instance = null;
        _currentProvider = AIProvider.None;
    }
}
