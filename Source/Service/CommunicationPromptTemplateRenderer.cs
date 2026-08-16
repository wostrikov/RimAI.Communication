using System;
using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Prompt;
using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication.Service;

sealed class CommunicationPromptTemplateRenderer : IPromptTemplateRenderer
{
    public bool TryRender(string template, PromptRenderRequest request, out string rendered, out string error)
    {
        rendered = template ?? string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(template))
            return false;

        try
        {
            var context = new PromptContext
            {
                CurrentPawn = request?.CurrentPawn as Pawn,
                AllPawns = (request?.Pawns ?? Array.Empty<object>()).OfType<Pawn>().ToList(),
                Map = request?.Map as Map,
                DialogueType = request?.DialogueType ?? "conversation",
                DialogueStatus = request?.DialogueStatus ?? "manual",
                PawnContext = request?.PawnContext ?? string.Empty,
                DialoguePrompt = request?.DialoguePrompt ?? string.Empty,
                ScopedPawnIndex = request?.ScopedPawnIndex ?? -1,
                IsPreview = false,
                VariableStore = PromptManager.Instance?.VariableStore ?? new VariableStore(),
                ChatHistory = new List<(Role role, string message)>()
            };
            if (context.AllPawns.Count == 0 && context.CurrentPawn != null)
                context.AllPawns.Add(context.CurrentPawn);
            if (context.Map == null)
                context.Map = context.CurrentPawn?.MapHeld ?? Find.CurrentMap;
            if (request?.ChatHistory != null)
            {
                foreach (var message in request.ChatHistory)
                {
                    if (!Enum.TryParse(message.Role, true, out Role role))
                        role = Role.User;
                    context.ChatHistory.Add((role, message.Content ?? string.Empty));
                }
            }

            rendered = ScribanParser.Render(template, context, logErrors: false) ?? string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            rendered = template ?? string.Empty;
            return false;
        }
    }
}
