using System.Diagnostics;
using ES.FX.Additions.Microsoft.AspNetCore.Middleware;

namespace ES.FX.Additions.Microsoft.AspNetCore.Tests;

/// <summary>
///     Functional regression coverage for <see cref="TraceIdResponseHeaderMiddleware" />, which writes an
///     <c>X-Trace-Id</c> response header from <see cref="Activity.Current" /> (falling back to the request's
///     <c>TraceIdentifier</c>).
/// </summary>
public class TraceIdResponseHeaderMiddlewareTests
{
    [Fact]
    public async Task Response_ContainsNonEmptyTraceIdHeader()
    {
        using var server = TestServerFactory.CreateWithMiddleware<TraceIdResponseHeaderMiddleware>();
        var response = await server.CreateClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out var values));
        var traceId = Assert.Single(values!);
        Assert.False(string.IsNullOrWhiteSpace(traceId));
    }

    [Fact]
    public async Task WhenActivityIsCurrent_TraceIdHeaderUsesActivityId()
    {
        // Force an ambient Activity so the middleware's OnStarting callback observes Activity.Current?.Id.
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("ES.FX.Tests.TraceId");
        using var activity = source.StartActivity("test-request");
        Assert.NotNull(activity);
        Assert.NotNull(Activity.Current);

        // The middleware reads Activity.Current at response-start time; the callback runs on the same
        // execution context that flows the ambient Activity into the pipeline for this in-process request.
        using var server = TestServerFactory.CreateWithMiddleware<TraceIdResponseHeaderMiddleware>();
        var response = await server.CreateClient().GetAsync("/", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out var values));
        var traceId = Assert.Single(values!);
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        // The emitted trace id must be a W3C-style activity id (starts with "00-") when an Activity is present.
        Assert.StartsWith("00-", traceId);
    }
}
