namespace Ustas.RimAI.Communication.Client.ProviderPolicy;

/// <summary>
/// Typed Communication provider-failure taxonomy. Semantic truth lives here,
/// not in exception-message matching at call sites.
/// </summary>
public enum CommunicationFailureClass
{
    None = 0,
    Configuration,
    Authentication,
    RateLimited,
    Timeout,
    Transport,
    ProviderUnavailable,
    MalformedResponse,
    EmptyResponse,
    Cancelled,
    Unknown
}

public readonly struct CommunicationFailureDisposition
{
    public CommunicationFailureDisposition(bool retrySameProvider, bool failover, bool terminal)
    {
        RetrySameProvider = retrySameProvider;
        Failover = failover;
        Terminal = terminal;
    }

    public bool RetrySameProvider { get; }
    public bool Failover { get; }
    public bool Terminal { get; }
}

/// <summary>
/// Deterministic failure policy. Same-provider transport retry stays in
/// <c>TextAiRetryPolicy</c>; this table only answers Communication-level
/// retry vs alternate-provider failover.
/// </summary>
public static class CommunicationFailurePolicy
{
    public static CommunicationFailureDisposition For(CommunicationFailureClass failureClass)
    {
        switch (failureClass)
        {
            case CommunicationFailureClass.None:
                return new CommunicationFailureDisposition(false, false, false);
            case CommunicationFailureClass.Configuration:
            case CommunicationFailureClass.Authentication:
            case CommunicationFailureClass.Cancelled:
                return new CommunicationFailureDisposition(false, false, true);
            case CommunicationFailureClass.RateLimited:
            case CommunicationFailureClass.Timeout:
            case CommunicationFailureClass.Transport:
            case CommunicationFailureClass.ProviderUnavailable:
            case CommunicationFailureClass.MalformedResponse:
            case CommunicationFailureClass.EmptyResponse:
            case CommunicationFailureClass.Unknown:
                return new CommunicationFailureDisposition(false, true, false);
            default:
                return new CommunicationFailureDisposition(false, true, false);
        }
    }
}
