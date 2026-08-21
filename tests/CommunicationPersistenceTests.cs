using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication;
using Ustas.RimAI.Communication.Client.OpenAI;
using Ustas.RimAI.Core.Configuration;

internal static class CommunicationPersistenceTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        const string luna = "gpt-5.6-luna";

        // TEST 1 — selected model roundtrip
        var openAi = NewSnapshot(false, true, 0,
            Config(AIProvider.OpenAI, luna));
        var loaded = CommunicationCloudSettingsPersistence.Roundtrip(openAi);
        T(loaded.CloudConfigs.Count == 1, "t1-count");
        T(loaded.CloudConfigs[0].Provider == AIProvider.OpenAI, "t1-provider");
        T(loaded.CloudConfigs[0].SelectedModel == luna, "t1-luna");
        T(loaded.CloudConfigs[0].ApiKey == "", "t1-openai-key-not-persisted");
        T(loaded.UseSimpleConfig == false, "t1-advanced");
        T(CommunicationCloudSettingsPersistence.SelectedModelDefault == "(choose model)", "t1-default-sentinel");
        T(CommunicationCloudSettingsPersistence.SelectedModelDefault != "gemma-4-26b-a4b-it", "t1-not-gemma-default");

        // TEST 2 — multiple configs preserve order/providers/models
        var multi = NewSnapshot(false, true, 0,
            Config(AIProvider.OpenAI, luna),
            Config(AIProvider.Google, "gemma-4-26b-a4b-it", apiKey: "google-fixture"),
            Config(AIProvider.Custom, "Custom", custom: "my-local-model", baseUrl: "http://127.0.0.1:11434"));
        var multiLoaded = CommunicationCloudSettingsPersistence.Roundtrip(multi);
        T(multiLoaded.CloudConfigs.Count == 3, "t2-count");
        T(multiLoaded.CloudConfigs[0].Provider == AIProvider.OpenAI && multiLoaded.CloudConfigs[0].SelectedModel == luna, "t2-0");
        T(multiLoaded.CloudConfigs[1].Provider == AIProvider.Google && multiLoaded.CloudConfigs[1].SelectedModel == "gemma-4-26b-a4b-it", "t2-1");
        T(multiLoaded.CloudConfigs[2].Provider == AIProvider.Custom && multiLoaded.CloudConfigs[2].CustomModelName == "my-local-model", "t2-2");
        T(multiLoaded.CloudConfigs[2].BaseUrl == "http://127.0.0.1:11434", "t2-custom-url");

        // TEST 3 — active index roundtrip + out-of-range normalization
        var indexed = NewSnapshot(false, true, 2,
            Config(AIProvider.OpenAI, luna),
            Config(AIProvider.Google, "gemma-4-26b-a4b-it", apiKey: "google-fixture"),
            Config(AIProvider.Player2, "Default"));
        var indexedLoaded = CommunicationCloudSettingsPersistence.Roundtrip(indexed);
        T(indexedLoaded.CurrentCloudConfigIndex == 2, "t3-index");
        T(indexedLoaded.CloudConfigs[2].Provider == AIProvider.Player2, "t3-active-provider");
        T(CommunicationCloudSettingsPersistence.NormalizeIndex(9, 3) == 0, "t3-oob-high");
        T(CommunicationCloudSettingsPersistence.NormalizeIndex(-1, 3) == 0, "t3-oob-low");
        T(CommunicationCloudSettingsPersistence.NormalizeIndex(0, 0) == 0, "t3-empty");
        var missingIndex = NewSnapshot(false, true, 0,
            Config(AIProvider.OpenAI, luna),
            Config(AIProvider.Google, "gemma-4-26b-a4b-it", apiKey: "g"));
        missingIndex.CurrentCloudConfigIndex = 0;
        T(CommunicationCloudSettingsPersistence.Roundtrip(missingIndex).CurrentCloudConfigIndex == 0, "t3-absent-defaults-zero");

        // Local provider config roundtrip
        var local = NewSnapshot(false, false, 0, Config(AIProvider.OpenAI, luna));
        local.LocalConfig = Config(AIProvider.Local, "llama3", baseUrl: "http://127.0.0.1:11434/v1");
        var localLoaded = CommunicationCloudSettingsPersistence.Roundtrip(local);
        T(localLoaded.UseCloudProviders == false, "t-local-mode");
        T(localLoaded.LocalConfig.Provider == AIProvider.Local, "t-local-provider");
        T(localLoaded.LocalConfig.BaseUrl.Contains("11434"), "t-local-url");

        // Choose-model legacy/default state
        var choose = NewSnapshot(false, true, 0, Config(AIProvider.OpenAI, null));
        var chooseLoaded = CommunicationCloudSettingsPersistence.Roundtrip(choose);
        T(chooseLoaded.CloudConfigs[0].SelectedModel == CommunicationCloudSettingsPersistence.SelectedModelDefault, "t-choose-default");

        // TEST 5 — close without edits leaves model unchanged
        var unchanged = CommunicationCloudSettingsPersistence.Roundtrip(openAi);
        T(unchanged.CloudConfigs[0].SelectedModel == luna, "t5-unchanged");
        T(unchanged.CurrentCloudConfigIndex == 0, "t5-index");

        // TEST 6 — same-provider reselect preserves luna
        var same = new CloudProviderSelectionState { Provider = AIProvider.OpenAI, SelectedModel = luna };
        T(CommunicationProviderSelection.ApplyProvider(same, AIProvider.OpenAI) == false, "t6-no-change");
        T(same.SelectedModel == luna, "t6-luna");

        // TEST 7 — actual provider change invalidates model
        var changed = new CloudProviderSelectionState { Provider = AIProvider.OpenAI, SelectedModel = luna };
        T(CommunicationProviderSelection.ApplyProvider(changed, AIProvider.Google), "t7-changed");
        T(changed.Provider == AIProvider.Google, "t7-provider");
        T(changed.SelectedModel == CommunicationCloudSettingsPersistence.SelectedModelDefault, "t7-reset");
        var toCustom = new CloudProviderSelectionState { Provider = AIProvider.OpenAI, SelectedModel = luna };
        CommunicationProviderSelection.ApplyProvider(toCustom, AIProvider.Custom);
        T(toCustom.SelectedModel == CommunicationProviderSelection.CustomModelSentinel, "t7-custom");
        var toPlayer2 = new CloudProviderSelectionState { Provider = AIProvider.OpenAI, SelectedModel = luna };
        CommunicationProviderSelection.ApplyProvider(toPlayer2, AIProvider.Player2);
        T(toPlayer2.SelectedModel == CommunicationProviderSelection.Player2DefaultModel, "t7-player2");

        // TEST 8 — model-list refresh preserves a valid selection
        T(CommunicationProviderSelection.PreserveSelectedModelOnRefresh(luna), "t8-preserve");
        T(CommunicationProviderSelection.ResolveSelectedModelAfterRefresh(luna, new List<string> { "gpt-4.1", "o3" }) == luna, "t8-keep-if-absent");
        T(CommunicationProviderSelection.ResolveSelectedModelAfterRefresh(luna, new List<string> { luna, "gpt-4.1" }) == luna, "t8-keep-if-present");
        T(CommunicationProviderSelection.ResolveSelectedModelAfterRefresh("(choose model)", new List<string> { luna }) == "(choose model)", "t8-unset-stays-unset");
        T(CommunicationProviderSelection.ModelMissingFromAuthoritativeList(luna, new List<string> { "gpt-4.1" }), "t8-missing-documented");
        T(!CommunicationProviderSelection.ModelMissingFromAuthoritativeList(luna, new List<string> { luna }), "t8-present");

        // Fallback is transient
        openAi.IsUsingFallbackModel = true;
        T(!CommunicationCloudSettingsPersistence.PersistIsUsingFallbackModel, "t-fallback-not-durable");
        T(!CommunicationCloudSettingsPersistence.Roundtrip(openAi).IsUsingFallbackModel, "t-fallback-reset");

        // TEST 9 — credential policy
        string oldRimai = Environment.GetEnvironmentVariable(AiCredentialResolver.Canonical);
        string oldApi = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        string oldTalk = Environment.GetEnvironmentVariable("OPENAI_RIMTALK");
        string oldChat = Environment.GetEnvironmentVariable("OPENAI_RIMCHAT");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "legacy-api-key");
            Environment.SetEnvironmentVariable("OPENAI_RIMTALK", "legacy-talk");
            Environment.SetEnvironmentVariable("OPENAI_RIMCHAT", "legacy-chat");
            Environment.SetEnvironmentVariable(AiCredentialResolver.Canonical, "canonical-rimai");
            var after = CommunicationCloudSettingsPersistence.Roundtrip(openAi);
            T(OpenAIProviderAdapter.CredentialPresent, "t9-rimai-present");
            T(AiCredentialResolver.Resolve().SourceName == AiCredentialResolver.Canonical, "t9-source");
            T(CommunicationCloudSettingsPersistence.IsCloudConfigValid(
                after.CloudConfigs[0].IsEnabled,
                after.CloudConfigs[0].Provider,
                after.CloudConfigs[0].SelectedModel,
                after.CloudConfigs[0].ApiKey,
                after.CloudConfigs[0].BaseUrl,
                after.UseCloudProviders,
                OpenAIProviderAdapter.CredentialPresent), "t9-openai-valid");

            Environment.SetEnvironmentVariable(AiCredentialResolver.Canonical, null);
            T(!OpenAIProviderAdapter.CredentialPresent, "t9-no-fallback-with-legacy-present");
            var legacyOnly = AiCredentialResolver.Resolve();
            T(legacyOnly.Value == null, "t9-legacy-value-null");
            T(!CommunicationCloudSettingsPersistence.IsCloudConfigValid(
                after.CloudConfigs[0].IsEnabled,
                after.CloudConfigs[0].Provider,
                after.CloudConfigs[0].SelectedModel,
                after.CloudConfigs[0].ApiKey,
                after.CloudConfigs[0].BaseUrl,
                after.UseCloudProviders,
                OpenAIProviderAdapter.CredentialPresent), "t9-invalid-without-rimai");
        }
        finally
        {
            Environment.SetEnvironmentVariable(AiCredentialResolver.Canonical, oldRimai);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldApi);
            Environment.SetEnvironmentVariable("OPENAI_RIMTALK", oldTalk);
            Environment.SetEnvironmentVariable("OPENAI_RIMCHAT", oldChat);
        }

        return n;
    }

    static CommunicationCloudSettingsSnapshot NewSnapshot(
        bool useSimple,
        bool useCloud,
        int index,
        params CloudConfigRecord[] configs)
    {
        var snapshot = new CommunicationCloudSettingsSnapshot
        {
            UseSimpleConfig = useSimple,
            UseCloudProviders = useCloud,
            CurrentCloudConfigIndex = index,
            LocalConfig = new CloudConfigRecord { Provider = AIProvider.Local }
        };
        foreach (var config in configs)
            snapshot.CloudConfigs.Add(config);
        return snapshot;
    }

    static CloudConfigRecord Config(
        AIProvider provider,
        string model,
        string custom = "",
        string baseUrl = "",
        string apiKey = "should-not-survive-openai")
    {
        return new CloudConfigRecord
        {
            IsEnabled = true,
            Provider = provider,
            SelectedModel = model,
            CustomModelName = custom,
            BaseUrl = baseUrl,
            ApiKey = apiKey
        };
    }
}
