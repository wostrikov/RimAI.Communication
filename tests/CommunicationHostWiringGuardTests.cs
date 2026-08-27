using System;
using System.IO;

internal static class CommunicationHostWiringGuardTests
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

        string errorHandler = Read("AIErrorHandler.cs.src");
        string service = Read("AIService.cs.src");
        string bubblePatch = Read("BubblePatch.cs.src");
        T(!string.IsNullOrWhiteSpace(errorHandler), "error-handler-copied");
        T(!string.IsNullOrWhiteSpace(service), "service-copied");
        T(errorHandler.Contains("CommunicationProviderOrchestrator.ExecuteAsync"), "handler-uses-orchestrator");
        T(!errorHandler.Contains("TryNextConfig"), "handler-no-settings-rotate");
        T(!errorHandler.Contains("IsUsingFallbackModel"), "handler-no-simple-flag-mutate");
        T(service.Contains("AIErrorHandler.ExecuteLogicalRequest"), "service-uses-logical-request");
        T(service.Contains("AIClientFactory.CreateClientAsync(slot)"), "service-creates-per-slot");
        T(!service.Contains("HandleWithRetry"), "service-no-legacy-retry-loop");
        T(!service.Contains("GetAIClientAsync()"), "service-no-singleton-client");
        int settingsGuard = bubblePatch.IndexOf(
            "!settings.IsEnabled || !settings.ProcessNonRimTalkInteractions",
            StringComparison.Ordinal);
        int renderCall = bubblePatch.IndexOf(
            "entry.ToGameStringFromPOV(initiator)",
            StringComparison.Ordinal);
        T(settingsGuard >= 0 && renderCall > settingsGuard, "bubble-render-after-settings-guard");
        T(bubblePatch.Contains("pawns.Length < 2"), "bubble-requires-two-pawns");
        T(bubblePatch.Contains("pawns[0].RaceProps?.Humanlike != true")
            && bubblePatch.Contains("pawns[1].RaceProps?.Humanlike != true"),
            "bubble-skips-animal-grammar-render");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
