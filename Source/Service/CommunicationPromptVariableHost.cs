using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.API;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication.Service;

sealed class CommunicationPromptVariableHost : IPromptVariableHost
{
    public IReadOnlyList<PromptCustomVariableDescriptor> GetCustomVariables()
    {
        var list = new List<PromptCustomVariableDescriptor>();
        foreach (var item in ContextHookRegistry.GetAllCustomVariables())
        {
            list.Add(new PromptCustomVariableDescriptor
            {
                Name = item.Name ?? string.Empty,
                ModId = item.ModId ?? string.Empty,
                Description = item.Description ?? string.Empty,
                Kind = item.Type ?? "Context"
            });
        }

        return list;
    }

    public void RegisterContextVariable(string modId, string name, Func<object?, string> provider, string? description, int priority)
    {
        if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(name) || provider == null)
            return;
        RimTalkPromptAPI.RegisterContextVariable(
            modId,
            name,
            ctx => provider(ctx) ?? string.Empty,
            description,
            priority);
    }

    public void UnregisterMod(string modId)
    {
        if (!string.IsNullOrEmpty(modId))
            ContextHookRegistry.UnregisterMod(modId);
    }

    public bool TryGetPawnVariable(string name, object? pawn, out string value)
    {
        value = string.Empty;
        return pawn is Pawn typed && ContextHookRegistry.TryGetPawnVariable(name, typed, out value);
    }

    public bool TryGetEnvironmentVariable(string name, object? map, out string value)
    {
        value = string.Empty;
        return map is Map typed && ContextHookRegistry.TryGetEnvironmentVariable(name, typed, out value);
    }

    public bool TryGetContextVariable(string name, PromptVariableResolveRequest? request, out string value)
    {
        value = string.Empty;
        var context = new PromptContext
        {
            CurrentPawn = request?.CurrentPawn as Pawn,
            AllPawns = (request?.Pawns ?? Array.Empty<object>()).OfType<Pawn>().ToList(),
            Map = request?.Map as Map
        };
        if (context.AllPawns.Count == 0 && context.CurrentPawn != null)
            context.AllPawns.Add(context.CurrentPawn);
        if (context.Map == null)
            context.Map = context.CurrentPawn?.MapHeld;
        context.VariableStore = PromptManager.Instance?.VariableStore ?? new VariableStore();
        return ContextHookRegistry.TryGetContextVariable(name, context, out value);
    }

    public string GetJsonInstruction()
    {
        var settings = Settings.Get();
        return Constant.GetJsonInstruction(settings?.ApplyMoodAndSocialEffects ?? false);
    }

    public int RemoveRuntimeVariables(Func<string, bool> predicate)
    {
        if (predicate == null)
            return 0;
        var store = RimTalkPromptAPI.GetVariableStore();
        if (store == null)
            return 0;
        var keys = store.GetAllVariables()?.Keys.ToList() ?? new List<string>();
        var removed = 0;
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key) || !predicate(key))
                continue;
            if (store.RemoveVar(key))
                removed++;
        }

        return removed;
    }
}
