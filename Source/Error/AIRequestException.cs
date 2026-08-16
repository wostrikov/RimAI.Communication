using System;
using Ustas.RimAI.Communication.Client;

namespace Ustas.RimAI.Communication.Error;

public class AIRequestException : Exception
{
    public Payload Payload { get; }

    public AIRequestException(string message, Payload payload) : base(message)
    {
        Payload = payload;
    }
}