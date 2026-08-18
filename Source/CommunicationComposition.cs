using HarmonyLib;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Core.Communication;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Configuration;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Communication;

/// <summary>
/// Module composition root for RimAI.Communication. Owns Harmony and Core access wiring.
/// Settings UI state remains on <see cref="Settings"/>.
/// </summary>
public sealed class CommunicationComposition : IRimAiModuleComposition
{
    public static CommunicationComposition Current { get; } = new();

    Settings? _mod;

    public string ModuleId => RimAiModuleIds.Communication;

    public bool IsStarted { get; private set; }

    public void Bind(Settings mod)
    {
        _mod = mod ?? throw new System.ArgumentNullException(nameof(mod));
    }

    public void Start()
    {
        if (IsStarted)
            return;
        if (_mod is null)
            throw new System.InvalidOperationException("CommunicationComposition.Bind must run before Start.");

        var harmony = new Harmony("ustas.rimai.communication");
        harmony.PatchAll();
        _mod.RegisterRimAIContributions();
        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
    }
}
