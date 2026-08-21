using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Client.OpenAI;
using Verse;

namespace Ustas.RimAI.Communication;

public class ApiConfig : IExposable
{
    public bool IsEnabled = true;
    public AIProvider Provider = AIProvider.Google;
    public string ApiKey = "";
    public string SelectedModel = CommunicationCloudSettingsPersistence.SelectedModelDefault;
    public string CustomModelName = "";
    public string BaseUrl = "";

    public void ExposeData()
    {
        Scribe_Values.Look(ref IsEnabled, "isEnabled", true);
        Scribe_Values.Look(ref Provider, "provider", AIProvider.Google);
        if (Provider != AIProvider.OpenAI) Scribe_Values.Look(ref ApiKey, "apiKey", "");
        else ApiKey = "";
        Scribe_Values.Look(ref SelectedModel, "selectedModel", CommunicationCloudSettingsPersistence.SelectedModelDefault);
        Scribe_Values.Look(ref CustomModelName, "customModelName", "");
        Scribe_Values.Look(ref BaseUrl, "baseUrl", "");
    }

    public bool IsValid()
    {
        return CommunicationCloudSettingsPersistence.IsCloudConfigValid(
            IsEnabled,
            Provider,
            SelectedModel,
            ApiKey,
            BaseUrl,
            Settings.Get().UseCloudProviders,
            Provider == AIProvider.OpenAI && OpenAIProviderAdapter.CredentialPresent);
    }
}
