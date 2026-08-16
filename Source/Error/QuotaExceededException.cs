using Ustas.RimAI.Communication.Client;

namespace Ustas.RimAI.Communication.Error;

public class QuotaExceededException : AIRequestException
{
    public QuotaExceededException(string message, Payload payload = null) : base(message, payload)
    {
    }
}