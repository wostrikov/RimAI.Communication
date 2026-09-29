using System;
using System.Threading;

namespace Ustas.RimAI.Communication.Error;

/// <summary>
/// Collapses a run of failures into one notice. The first failure opens a notice; failures that
/// come while it is waiting to be shown, or within the quiet period after it was shown, are only
/// counted, and the count goes into the next notice. Every failure is still logged by the caller;
/// this only decides what the player sees. Safe to call from any thread.
/// </summary>
public sealed class FailureNoticeGate(TimeSpan quietPeriod)
{
    private int _pending;
    private int _noticeQueued;
    private long _lastShownTicks = long.MinValue;

    /// <summary>
    /// Records one failure. True when the caller should queue a notice; false when the failure
    /// was folded into one already queued, or into the next.
    /// </summary>
    public bool Record(DateTime now)
    {
        Interlocked.Increment(ref _pending);
        long last = Interlocked.Read(ref _lastShownTicks);
        if (last != long.MinValue && now.Ticks - last < quietPeriod.Ticks) return false;
        return Interlocked.CompareExchange(ref _noticeQueued, 1, 0) == 0;
    }

    /// <summary>Called when the notice is shown: how many failures it stands for.</summary>
    public int TakeForNotice(DateTime now)
    {
        Interlocked.Exchange(ref _lastShownTicks, now.Ticks);
        Interlocked.Exchange(ref _noticeQueued, 0);
        return Interlocked.Exchange(ref _pending, 0);
    }
}
