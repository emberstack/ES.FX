using System.Globalization;
using ES.FX.Additions.Microsoft.AspNetCore.Middleware;

namespace ES.FX.Additions.Microsoft.AspNetCore.Tests;

/// <summary>
///     Functional regression coverage for <see cref="ServerTimingMiddleware" />, which writes a
///     <c>Server-Timing: total;dur=&lt;ms&gt;</c> response header measuring request duration.
/// </summary>
public class ServerTimingMiddlewareTests
{
    [Fact]
    public async Task Response_ContainsServerTimingHeader()
    {
        using var server = TestServerFactory.CreateWithMiddleware<ServerTimingMiddleware>();
        var response = await server.CreateClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Server-Timing", out var values));
        var header = Assert.Single(values!);
        Assert.StartsWith("total;dur=", header);
    }

    [Fact]
    public async Task ServerTiming_DurationIsParsableInvariantNonNegativeNumber()
    {
        using var server = TestServerFactory.CreateWithMiddleware<ServerTimingMiddleware>();
        var response = await server.CreateClient().GetAsync("/", TestContext.Current.CancellationToken);

        var header = response.Headers.GetValues("Server-Timing").Single();
        var durText = header["total;dur=".Length..];

        Assert.True(double.TryParse(durText, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur),
            $"Duration '{durText}' should parse as an invariant-culture number.");
        Assert.True(dur >= 0, "Elapsed duration must be non-negative.");
    }
}