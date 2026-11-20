using System;
using Ustas.RimAI.Communication.Client;

namespace Ustas.RimAI.Communication.Error;

public class AIRequestException : Exception
{
    public Payload Payload { get; }

    /// <summary>The provider's HTTP status, or 0 when the request never got one.</summary>
    public int StatusCode { get; }

    /// <summary>The shared transport's error kind ("http_error", "transport_error", ...), or null.</summary>
    public string ErrorKind { get; }

    /// <summary>The provider's own error code ("model_not_found", "invalid_api_key", ...), or null.</summary>
    public string ErrorCode { get; }

    public AIRequestException(string message, Payload payload, int statusCode = 0, string errorKind = null,
        string errorCode = null) : base(message)
    {
        Payload = payload;
        StatusCode = statusCode;
        ErrorKind = errorKind;
        ErrorCode = errorCode;
    }
}
