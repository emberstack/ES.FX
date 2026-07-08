using System.Diagnostics;
using ES.FX.OpenData.Romania.Fiscal.Anaf.Internal;

namespace ES.FX.OpenData.Romania.Fiscal.Anaf.Tests;

public class AnafRequestThrottleTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Spaces_consecutive_requests_by_the_configured_interval()
    {
        var throttle = new AnafRequestThrottle(TimeSpan.FromMilliseconds(150));

        var stopwatch = Stopwatch.StartNew();
        await throttle.WaitAsync(Ct); // first: no wait
        await throttle.WaitAsync(Ct); // second: waits out the interval
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds >= 100,
            $"expected the second request to be delayed; elapsed {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Zero_interval_disables_throttling()
    {
        var throttle = new AnafRequestThrottle(TimeSpan.Zero);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) await throttle.WaitAsync(Ct);
        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }
}
