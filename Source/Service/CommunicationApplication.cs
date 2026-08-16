using Ustas.RimAI.Core.Communication;
using Verse;

namespace Ustas.RimAI.Communication.Service;

public sealed class CommunicationApplication : ICommunicationApplication
{
    public bool TryExecuteDialogue(object initiator, object recipient, string message)
    {
        if (initiator is not Pawn actor || recipient is not Pawn other)
            return false;
        CustomDialogueService.ExecuteDialogue(actor, other, message ?? string.Empty);
        return true;
    }
}
