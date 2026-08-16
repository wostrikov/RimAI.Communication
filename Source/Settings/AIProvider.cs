using System.Collections.Generic;
using Ustas.RimAI.Core.Configuration;

namespace Ustas.RimAI.Communication;

public enum AIProvider
{
    Google,
    OpenAI,
    DeepSeek,
    Grok,
    GLM,
    GLMCoding,
    AlibabaIntl,
    AlibabaCN,
    OpenRouter,
    Player2,
    Local,
    Custom,
    None
}

public static class AIProviderRegistry
{
    public static string GetLabel(this AIProvider p) => GameplayTextAiProviderCatalog.Label(p.ToString());

    public static string GetEndpointUrl(this AIProvider p) =>
        GameplayTextAiProviderCatalog.ChatEndpoint(p.ToString());

    public static string GetListModelsUrl(this AIProvider p) =>
        GameplayTextAiProviderCatalog.ListModelsUrl(p.ToString());

    public static IReadOnlyDictionary<string, string> GetExtraHeaders(this AIProvider p) =>
        GameplayTextAiProviderCatalog.ExtraHeaders(p.ToString());
}
