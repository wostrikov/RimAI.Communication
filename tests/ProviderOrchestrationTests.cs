using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ustas.RimAI.Communication;
using Ustas.RimAI.Communication.Client.OpenAI;
using Ustas.RimAI.Communication.Client.ProviderPolicy;

internal static class ProviderOrchestrationTests
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

        var google = Config(AIProvider.Google, "gemma-a", apiKey: "g-key");
        var deepseek = Config(AIProvider.DeepSeek, "deepseek-chat", apiKey: "d-key");
        var grok = Config(AIProvider.Grok, "grok-3", apiKey: "x-key");
        var local = Config(AIProvider.Local, "llama3", baseUrl: "http://127.0.0.1:11434/v1");
        var disabled = Config(AIProvider.OpenAI, "gpt-5.6-luna");
        disabled.IsEnabled = false;
        var unset = Config(AIProvider.Google, CommunicationCloudSettingsPersistence.SelectedModelDefault, apiKey: "g");

        var ordered = CommunicationProviderChain.Build(Request(false, true, 1, local, google, deepseek, grok));
        T(ordered.Count == 4, "order-count");
        T(ordered[0].SlotId == "cloud:1" && ordered[0].Provider == AIProvider.DeepSeek, "order-start-index");
        T(ordered[1].Provider == AIProvider.Grok, "order-wrap-grok");
        T(ordered[2].Provider == AIProvider.Google, "order-wrap-google");
        T(ordered[3].SlotId == "local" && ordered[3].Provider == AIProvider.Local, "order-local-last");

        var skipped = CommunicationProviderChain.Build(Request(false, true, 0, null, disabled, unset, deepseek));
        T(skipped.Count == 1 && skipped[0].Provider == AIProvider.DeepSeek, "disabled-unconfigured-skipped");

        var localOnly = CommunicationProviderChain.Build(Request(false, false, 0, local, google));
        T(localOnly.Count == 1 && localOnly[0].Provider == AIProvider.Local, "local-only");

        var simple = CommunicationProviderChain.Build(new CommunicationProviderChainRequest
        {
            Settings = new CommunicationCloudSettingsSnapshot { UseSimpleConfig = true },
            SimpleApiKey = "simple-key",
            SimpleDefaultModel = "gemma-4-26b-a4b-it",
            SimpleFallbackModel = "gemma-4-31b-it"
        });
        T(simple.Count == 2 && simple[0].SlotId == "simple:default" && simple[1].SlotId == "simple:fallback", "simple-two-slots");

        var none = CommunicationProviderChain.Build(new CommunicationProviderChainRequest
        {
            Settings = new CommunicationCloudSettingsSnapshot { UseSimpleConfig = true }
        });
        T(none.Count == 0, "no-providers");

        T(!CommunicationFailurePolicy.For(CommunicationFailureClass.Authentication).Failover, "auth-no-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.Authentication).Terminal, "auth-terminal");
        T(!CommunicationFailurePolicy.For(CommunicationFailureClass.Configuration).Failover, "config-no-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.Cancelled).Terminal, "cancel-terminal");
        T(!CommunicationFailurePolicy.For(CommunicationFailureClass.Cancelled).Failover, "cancel-no-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.RateLimited).Failover, "rate-failover");
        T(!CommunicationFailurePolicy.For(CommunicationFailureClass.RateLimited).RetrySameProvider, "rate-no-same-retry");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.Timeout).Failover, "timeout-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.Transport).Failover, "transport-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.MalformedResponse).Failover, "malformed-failover");
        T(CommunicationFailurePolicy.For(CommunicationFailureClass.EmptyResponse).Failover, "empty-failover");
        T(CommunicationFailureClassifier.FromHttp(429, null, false, false) == CommunicationFailureClass.RateLimited, "classify-429");
        T(CommunicationFailureClassifier.FromHttp(0, "credential_missing", false, false) == CommunicationFailureClass.Configuration, "classify-credential");
        T(CommunicationFailureClassifier.FromOpenAICategory(OpenAIErrorCategory.Authentication) == CommunicationFailureClass.Authentication, "classify-openai-auth");
        T(CommunicationFailureClassifier.FromException(new TimeoutException("x")) == CommunicationFailureClass.Timeout, "classify-timeout");
        T(CommunicationFailureClassifier.FromException(new OperationCanceledException()) == CommunicationFailureClass.Cancelled, "classify-cancel");
        T(CommunicationFailureClassifier.FromErrorKind("arbiter_reset") == CommunicationFailureClass.Cancelled, "classify-arbiter-reset");
        T(CommunicationFailureClassifier.FromErrorKind("arbiter_cancelled") == CommunicationFailureClass.Cancelled, "classify-arbiter-cancelled");
        T(CommunicationFailureClassifier.FromErrorKind("arbiter_queue_full") != CommunicationFailureClass.Cancelled, "arbiter-queue-full-is-not-cancel");

        var invoked = new List<string>();
        var primary = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            return CommunicationProviderAttempt.Success("ok-a");
        });
        T(primary.Succeeded && primary.SuccessfulProvider.StartsWith("Google"), "primary-success");
        T(invoked.Count == 1 && invoked[0] == "p0", "primary-b-not-invoked");

        invoked.Clear();
        var failover = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.DiagnosticName);
            if (slot.Provider == AIProvider.Google)
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Transport);
            return CommunicationProviderAttempt.Success("ok-b");
        });
        T(failover.Succeeded && failover.SuccessfulProvider.StartsWith("DeepSeek"), "failover-success-b");
        T(invoked.Count == 2, "failover-invoked-both");
        T(failover.Attempts[0].Kind == CommunicationAttemptKind.ProviderFailover, "failover-kind");

        invoked.Clear();
        var third = Run(Chain(google, deepseek, grok), slot =>
        {
            invoked.Add(slot.Provider.ToString());
            if (slot.Provider != AIProvider.Grok)
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.ProviderUnavailable);
            return CommunicationProviderAttempt.Success("ok-c");
        });
        T(third.Succeeded && third.SuccessfulProvider.StartsWith("Grok") && invoked.Count == 3, "three-then-success");

        invoked.Clear();
        var exhausted = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Transport);
        });
        T(!exhausted.Succeeded && exhausted.ProvidersExhausted, "exhausted");
        T(exhausted.TerminalClass == CommunicationFailureClass.Transport, "exhausted-class");
        T(invoked.Count == 2, "exhausted-both-tried");

        invoked.Clear();
        var auth = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Authentication);
        });
        T(!auth.Succeeded && auth.TerminalClass == CommunicationFailureClass.Authentication, "auth-stops");
        T(invoked.Count == 1, "auth-no-b");

        invoked.Clear();
        var rate = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            if (slot.Provider == AIProvider.Google)
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.RateLimited);
            return CommunicationProviderAttempt.Success("ok-rate");
        });
        T(rate.Succeeded && invoked.Count == 2, "rate-failsover");

        invoked.Clear();
        var timeout = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            if (slot.Provider == AIProvider.Google)
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.Timeout);
            return CommunicationProviderAttempt.Success("ok-timeout");
        });
        T(timeout.Succeeded && invoked.Count == 2, "timeout-failsover");

        invoked.Clear();
        var malformed = Run(Chain(google, deepseek), slot =>
        {
            invoked.Add(slot.SlotId);
            if (slot.Provider == AIProvider.Google)
                return CommunicationProviderAttempt.Fail(CommunicationFailureClass.MalformedResponse, text: "not-json");
            return CommunicationProviderAttempt.Success("{\"name\":\"A\",\"text\":\"Hi\"}");
        });
        T(malformed.Succeeded && invoked.Count == 2, "malformed-failsover");
        T(TalkResponseRecovery.Evaluate("not-json") == TalkResponseEvaluation.Malformed, "malformed-not-success");
        T(TalkResponseRecovery.Evaluate("") == TalkResponseEvaluation.Empty, "empty-not-success");

        invoked.Clear();
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            var cancelled = CommunicationProviderOrchestrator.ExecuteAsync(
                Chain(google, deepseek),
                slot =>
                {
                    invoked.Add(slot.SlotId);
                    return Task.FromResult(CommunicationProviderAttempt.Success("nope"));
                },
                "cancel-req",
                cts.Token).GetAwaiter().GetResult();
            T(!cancelled.Succeeded && cancelled.TerminalClass == CommunicationFailureClass.Cancelled, "cancel-result");
            T(invoked.Count == 0, "cancel-b-not-invoked");
        }

        var emptyChain = CommunicationProviderOrchestrator.ExecuteAsync(
            Array.Empty<CommunicationProviderSlot>(),
            slot => Task.FromResult(CommunicationProviderAttempt.Success("nope")),
            "empty",
            CancellationToken.None).GetAwaiter().GetResult();
        T(!emptyChain.Succeeded && emptyChain.TerminalClass == CommunicationFailureClass.Configuration, "no-providers-terminal");

        return n;
    }

    static CommunicationProviderOutcome Run(
        IReadOnlyList<CommunicationProviderSlot> chain,
        Func<CommunicationProviderSlot, CommunicationProviderAttempt> attempt) =>
        CommunicationProviderOrchestrator.ExecuteAsync(
            chain,
            slot => Task.FromResult(attempt(slot)),
            "test",
            CancellationToken.None).GetAwaiter().GetResult();

    static List<CommunicationProviderSlot> Chain(params CloudConfigRecord[] configs)
    {
        var slots = new List<CommunicationProviderSlot>();
        for (var i = 0; i < configs.Length; i++)
        {
            slots.Add(new CommunicationProviderSlot
            {
                SlotId = "p" + i,
                Provider = configs[i].Provider,
                SelectedModel = configs[i].SelectedModel,
                CustomModelName = configs[i].CustomModelName,
                BaseUrl = configs[i].BaseUrl,
                ApiKey = configs[i].ApiKey
            });
        }

        return slots;
    }

    static CommunicationProviderChainRequest Request(
        bool simple,
        bool cloud,
        int index,
        CloudConfigRecord local,
        params CloudConfigRecord[] configs)
    {
        var snapshot = new CommunicationCloudSettingsSnapshot
        {
            UseSimpleConfig = simple,
            UseCloudProviders = cloud,
            CurrentCloudConfigIndex = index,
            LocalConfig = local ?? new CloudConfigRecord { Provider = AIProvider.Local }
        };
        foreach (var config in configs)
            snapshot.CloudConfigs.Add(config);
        return new CommunicationProviderChainRequest
        {
            Settings = snapshot,
            OpenAiCredentialPresent = true,
            AllowLocalFailover = true
        };
    }

    static CloudConfigRecord Config(AIProvider provider, string model, string apiKey = "", string baseUrl = "") =>
        new CloudConfigRecord
        {
            IsEnabled = true,
            Provider = provider,
            SelectedModel = model,
            CustomModelName = provider == AIProvider.Local ? model : "",
            BaseUrl = baseUrl,
            ApiKey = apiKey
        };
}
