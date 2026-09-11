using System;

namespace Ustas.RimAI.Communication.Service;

/// <summary>
/// Backstop for AIService's busy flag: held longer than any request could legitimately take,
/// it is treated as stuck and released rather than leaving the colony silent for good.
/// </summary>
public static class BusyGate
{
    // The client allows 60s to connect plus 60s of read inactivity, with retries and provider
    // failover on top, so a slow model on a bad connection can take minutes. 300s is well past that.
    public const int StuckAfterSeconds = 300;

    public static bool IsStuck(bool busy, DateTime? busySince, DateTime now,
        int stuckAfterSeconds = StuckAfterSeconds)
    {
        if (!busy) return false;

        // No stamp means nobody recorded the start; never release a request we know nothing about.
        if (busySince == null) return false;

        return (now - busySince.Value).TotalSeconds >= stuckAfterSeconds;
    }
}
