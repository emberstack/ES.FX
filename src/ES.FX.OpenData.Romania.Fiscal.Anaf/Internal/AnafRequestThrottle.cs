using System.Diagnostics;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

/// <summary>
///     A process-wide throttle that spaces outgoing ANAF requests at least <c>minInterval</c> apart, shared as a
///     singleton across all consumers so they respect one budget. ANAF throttles per source IP (~1 req/s).
/// </summary>
internal sealed class AnafRequestThrottle(TimeSpan minInterval)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _lastTimestamp;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        if (minInterval <= TimeSpan.Zero) return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_lastTimestamp != 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(_lastTimestamp);
                var remaining = minInterval - elapsed;
                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }

            _lastTimestamp = Stopwatch.GetTimestamp();
        }
        finally
        {
            _gate.Release();
        }
    }
}
