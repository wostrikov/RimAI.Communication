using System;
using Ustas.RimAI.Communication.Client.OpenAI;

namespace Ustas.RimAI.Communication.Client.ProviderPolicy;

/// <summary>
/// Maps transport/status/exception signals onto <see cref="CommunicationFailureClass"/>.
/// Does not decide retry or failover — see <see cref="CommunicationFailurePolicy"/>.
/// </summary>
public static class CommunicationFailureClassifier
{
    public static CommunicationFailureClass FromHttp(int statusCode, string errorKind, bool timedOut, bool cancelled)
    {
        if (cancelled)
            return CommunicationFailureClass.Cancelled;
        if (timedOut)
            return CommunicationFailureClass.Timeout;

        var fromKind = FromErrorKind(errorKind);
        if (fromKind != CommunicationFailureClass.Unknown && fromKind != CommunicationFailureClass.None)
            return fromKind;

        if (statusCode == 401 || statusCode == 403)
            return CommunicationFailureClass.Authentication;
        if (statusCode == 429)
            return CommunicationFailureClass.RateLimited;
        if (statusCode >= 500)
            return CommunicationFailureClass.ProviderUnavailable;
        if (statusCode == 408)
            return CommunicationFailureClass.Timeout;
        if (statusCode > 0)
            return CommunicationFailureClass.Transport;
        return CommunicationFailureClass.Unknown;
    }

    public static CommunicationFailureClass FromErrorKind(string errorKind)
    {
        if (string.IsNullOrWhiteSpace(errorKind))
            return CommunicationFailureClass.Unknown;

        switch (errorKind.Trim().ToLowerInvariant())
        {
            case "credential_missing":
            case "missing_endpoint":
            case "not_attempted":
                return CommunicationFailureClass.Configuration;
            case "empty_output":
            case "empty_transport":
                return CommunicationFailureClass.EmptyResponse;
            case "cancelled":
            case "canceled":
            // The shared request arbiter dropped the request before it ran: its caller
            // cancelled, or the game session was reset under it (a save being loaded).
            case "arbiter_cancelled":
            case "arbiter_reset":
                return CommunicationFailureClass.Cancelled;
            case "timeout":
                return CommunicationFailureClass.Timeout;
            case "transport_error":
                return CommunicationFailureClass.Transport;
            case "secret_in_payload":
                return CommunicationFailureClass.MalformedResponse;
            default:
                return CommunicationFailureClass.Unknown;
        }
    }

    public static CommunicationFailureClass FromOpenAICategory(OpenAIErrorCategory category)
    {
        switch (category)
        {
            case OpenAIErrorCategory.Authentication:
            case OpenAIErrorCategory.Permission:
                return CommunicationFailureClass.Authentication;
            case OpenAIErrorCategory.RateLimit:
                return CommunicationFailureClass.RateLimited;
            case OpenAIErrorCategory.Server:
                return CommunicationFailureClass.ProviderUnavailable;
            case OpenAIErrorCategory.InvalidRequest:
            case OpenAIErrorCategory.UnsupportedParameter:
            case OpenAIErrorCategory.ModelNotFound:
                return CommunicationFailureClass.Configuration;
            default:
                return CommunicationFailureClass.Unknown;
        }
    }

    public static CommunicationFailureClass FromException(Exception exception)
    {
        if (exception == null)
            return CommunicationFailureClass.Unknown;
        if (exception is OperationCanceledException)
            return CommunicationFailureClass.Cancelled;
        if (exception is TimeoutException)
            return CommunicationFailureClass.Timeout;

        var name = exception.GetType().Name ?? string.Empty;
        if (name.IndexOf("Quota", StringComparison.OrdinalIgnoreCase) >= 0)
            return CommunicationFailureClass.RateLimited;
        if (name.IndexOf("AIRequest", StringComparison.OrdinalIgnoreCase) >= 0)
            return FromHttp(0, null, false, false);

        var message = exception.Message ?? string.Empty;
        if (message.IndexOf("401", StringComparison.Ordinal) >= 0 ||
            message.IndexOf("403", StringComparison.Ordinal) >= 0 ||
            message.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0)
            return CommunicationFailureClass.Authentication;
        if (message.IndexOf("429", StringComparison.Ordinal) >= 0 ||
            message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
            return CommunicationFailureClass.RateLimited;
        if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
            return CommunicationFailureClass.Timeout;
        return CommunicationFailureClass.Unknown;
    }

    public static CommunicationFailureClass FromPayload(string errorMessage, string response, int deliveredTalkObjects)
    {
        if (deliveredTalkObjects > 0)
            return CommunicationFailureClass.None;
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            if (errorMessage.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
                return CommunicationFailureClass.RateLimited;
            return CommunicationFailureClass.Unknown;
        }

        var evaluation = TalkResponseRecovery.Evaluate(response);
        if (evaluation == TalkResponseEvaluation.Recovered)
            return CommunicationFailureClass.None;
        if (evaluation == TalkResponseEvaluation.Empty)
            return CommunicationFailureClass.EmptyResponse;
        return CommunicationFailureClass.MalformedResponse;
    }
}
