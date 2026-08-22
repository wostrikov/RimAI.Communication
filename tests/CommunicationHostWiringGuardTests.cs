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
        T(!string.IsNullOrWhiteSpace(errorHandler), "error-handler-copied");
        T(!string.IsNullOrWhiteSpace(service), "service-copied");
        T(errorHandler.Contains("CommunicationProviderOrchestrator.ExecuteAsync"), "handler-uses-orchestrator");
        T(!errorHandler.Contains("TryNextConfig"), "handler-no-settings-rotate");
        T(!errorHandler.Contains("IsUsingFallbackModel"), "handler-no-simple-flag-mutate");
        T(service.Contains("AIErrorHandler.ExecuteLogicalRequest"), "service-uses-logical-request");
        T(service.Contains("AIClientFactory.CreateClientAsync(slot)"), "service-creates-per-slot");
        T(!service.Contains("HandleWithRetry"), "service-no-legacy-retry-loop");
        T(!service.Contains("GetAIClientAsync()"), "service-no-singleton-client");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
