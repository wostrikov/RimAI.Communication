using System;
using System.IO;
using System.Text;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Modules;
using Ustas.RimAI.Core.Storage;
using Verse;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Request-gated live proof for Communication model persistence.
/// Idle when the request file is absent. No ordinary-play side effects.
/// </summary>
internal static class RimAICommunicationPersistenceLiveProbe
{
    public const string RequestFileName = "RimAI.CommunicationPersistence.request.json";
    public const string ResultFileName = "RimAI.CommunicationPersistence.result.json";

    static bool _ran;

    internal static void TryRun()
    {
        if (_ran)
            return;
        var requestPath = Path.Combine(GenFilePaths.SaveDataFolderPath, RequestFileName);
        if (!LocalStorage.Current.FileExists(requestPath))
            return;
        _ran = true;

        var raw = LocalStorage.Current.ReadAllText(requestPath);
        var mode = Extract(raw, "mode") ?? "observe";
        var settings = Settings.Get();
        if (string.Equals(mode, "apply-luna", StringComparison.OrdinalIgnoreCase))
            ApplyLuna(settings);
        else if (string.Equals(mode, "apply-multiconfig", StringComparison.OrdinalIgnoreCase))
            ApplyMultiConfig(settings);

        if (mode.StartsWith("apply-", StringComparison.OrdinalIgnoreCase))
            RimAISettingsHostCommit.Commit();

        WriteResult(mode, settings, ok: true, error: null);
        LocalStorage.Current.DeleteFile(requestPath);
    }

    static void ApplyLuna(CommunicationSettings settings)
    {
        settings.UseSimpleConfig = false;
        settings.UseCloudProviders = true;
        if (settings.CloudConfigs == null || settings.CloudConfigs.Count == 0)
            settings.CloudConfigs.Add(new ApiConfig());
        var config = settings.CloudConfigs[0];
        CommunicationCloudApiSettingsPage.ApplyProviderSelection(config, AIProvider.OpenAI);
        config.Provider = AIProvider.OpenAI;
        config.SelectedModel = "gpt-5.6-luna";
        config.IsEnabled = true;
        settings.CurrentCloudConfigIndex = 0;
        settings.NormalizeActiveCloudConfigIndex();
    }

    static void ApplyMultiConfig(CommunicationSettings settings)
    {
        ApplyLuna(settings);
        while (settings.CloudConfigs.Count < 2)
            settings.CloudConfigs.Add(new ApiConfig());
        var second = settings.CloudConfigs[1];
        CommunicationCloudApiSettingsPage.ApplyProviderSelection(second, AIProvider.Player2);
        second.Provider = AIProvider.Player2;
        second.SelectedModel = CommunicationProviderSelection.Player2DefaultModel;
        second.IsEnabled = true;
        settings.CurrentCloudConfigIndex = 1;
        settings.NormalizeActiveCloudConfigIndex();
    }

    static void WriteResult(string mode, CommunicationSettings settings, bool ok, string error)
    {
        var xmlPath = Path.Combine(GenFilePaths.ConfigFolderPath, "Mod_RimAI.Communication_Settings.xml");
        var xml = LocalStorage.Current.FileExists(xmlPath)
            ? Redact(LocalStorage.Current.ReadAllText(xmlPath))
            : "";
        var active = settings?.GetActiveConfig();
        var sb = new StringBuilder();
        sb.Append("{\"schema_version\":1");
        sb.Append(",\"ok\":").Append(ok ? "true" : "false");
        sb.Append(",\"mode\":\"").Append(Esc(mode)).Append('"');
        if (!string.IsNullOrEmpty(error))
            sb.Append(",\"error\":\"").Append(Esc(error)).Append('"');
        sb.Append(",\"captured_at_utc\":\"").Append(DateTime.UtcNow.ToString("o")).Append('"');
        sb.Append(",\"useSimpleConfig\":").Append((settings?.UseSimpleConfig ?? true).ToString().ToLowerInvariant());
        sb.Append(",\"useCloudProviders\":").Append((settings?.UseCloudProviders ?? true).ToString().ToLowerInvariant());
        sb.Append(",\"currentCloudConfigIndex\":").Append(settings?.CurrentCloudConfigIndex ?? 0);
        sb.Append(",\"cloudConfigCount\":").Append(settings?.CloudConfigs?.Count ?? 0);
        sb.Append(",\"activeProvider\":\"").Append(Esc(active?.Provider.ToString())).Append('"');
        sb.Append(",\"activeSelectedModel\":\"").Append(Esc(active?.SelectedModel)).Append('"');
        sb.Append(",\"activeConfigNull\":").Append(active == null ? "true" : "false");
        sb.Append(",\"openAiCredentialPresent\":").Append(Client.OpenAI.OpenAIProviderAdapter.CredentialPresent.ToString().ToLowerInvariant());
        sb.Append(",\"xmlPath\":\"").Append(Esc(xmlPath)).Append('"');
        sb.Append(",\"xmlContainsLuna\":").Append(xml.Contains("gpt-5.6-luna") ? "true" : "false");
        sb.Append(",\"xmlContainsCurrentIndex\":").Append(xml.IndexOf("currentCloudConfigIndex", StringComparison.OrdinalIgnoreCase) >= 0 ? "true" : "false");
        sb.Append(",\"xmlExcerpt\":\"").Append(Esc(Excerpt(xml))).Append('"');
        sb.Append('}');
        LocalStorage.Current.WriteAllText(
            Path.Combine(GenFilePaths.SaveDataFolderPath, ResultFileName),
            sb.ToString());
        RimAiLog.Info(
            RimAiLogCategory.Communication,
            "[RimAI.Communication] persistence live probe mode=" + mode
            + " active=" + (active?.Provider + "/" + active?.SelectedModel ?? "null")
            + " index=" + (settings?.CurrentCloudConfigIndex ?? -1));
    }

    static string Excerpt(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return "";
        const int max = 1800;
        return xml.Length <= max ? xml : xml.Substring(0, max);
    }

    static string Redact(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return xml;
        return System.Text.RegularExpressions.Regex.Replace(
            xml,
            "<apiKey>[^<]*</apiKey>",
            "<apiKey>REDACTED</apiKey>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    static string Extract(string json, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            json ?? "",
            "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    static string Esc(string value) =>
        (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
