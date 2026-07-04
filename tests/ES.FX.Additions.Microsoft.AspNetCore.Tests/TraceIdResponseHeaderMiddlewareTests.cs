using System.Diagnostics;
using ES.FX.Additions.Microsoft.AspNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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
            Sample = (ref _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllData
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

    /// <summary>
    ///     Isolates the fallback branch (<c>Activity.Current?.Id ?? context.TraceIdentifier</c>): with
    ///     <see cref="Activity.Current" /> forced to <c>null</c> at response-start time, the <c>X-Trace-Id</c>
    ///     header must equal the request's <see cref="HttpContext.TraceIdentifier" />. A regression that dropped
    ///     the fallback (e.g. emitting an empty value when no Activity is present) fails this test.
    /// </summary>
    [Fact]
    public async Task WhenNoActivity_TraceIdHeaderFallsBackToTraceIdentifier()
    {
        var (context, feature) = CreateContext();
        context.TraceIdentifier = "trace-identifier-42";

        var middleware = new TraceIdResponseHeaderMiddleware(_ => Task.CompletedTask);

        // Ensure no ambient Activity is flowing when the middleware runs and when the callback fires.
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            await middleware.InvokeAsync(context);
            Assert.NotNull(feature.OnStartingCallback);

            // The middleware registered its callback but did not execute it yet.
            Assert.False(context.Response.Headers.ContainsKey("X-Trace-Id"));

            // Fire the response-start callback with Activity.Current still null.
            await feature.FireOnStartingAsync();
        }
        finally
        {
            Activity.Current = previous;
        }

        Assert.Equal("trace-identifier-42", context.Response.Headers["X-Trace-Id"].ToString());
    }

    /// <summary>
    ///     Isolates the primary branch deterministically (no TestServer hosting Activity in the mix): with a
    ///     known ambient <see cref="Activity" />, the header must equal that Activity's <see cref="Activity.Id" />
    ///     and must NOT fall back to <see cref="HttpContext.TraceIdentifier" />.
    /// </summary>
    [Fact]
    public async Task WhenActivityPresent_TraceIdHeaderUsesActivityId_NotTraceIdentifier()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = new ActivitySource("ES.FX.Tests.TraceId.Unit");
        using var activity = source.StartActivity("unit-request");
        Assert.NotNull(activity);
        Assert.NotNull(activity!.Id);

        var (context, feature) = CreateContext();
        context.TraceIdentifier = "should-not-be-used";

        var middleware = new TraceIdResponseHeaderMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        Assert.NotNull(feature.OnStartingCallback);
        await feature.FireOnStartingAsync();

        var header = context.Response.Headers["X-Trace-Id"].ToString();
        Assert.Equal(activity.Id, header);
        Assert.NotEqual("should-not-be-used", header);
    }

    private static (HttpContext Context, CapturingResponseFeature Feature) CreateContext()
    {
        var context = new DefaultHttpContext();
        var feature = new CapturingResponseFeature(context.Response.Headers);
        // Replace the default response feature so we can capture and manually fire OnStarting callbacks.
        context.Features.Set<IHttpResponseFeature>(feature);
        return (context, feature);
    }

    /// <summary>
    ///     A minimal <see cref="IHttpResponseFeature" /> that captures the <c>OnStarting</c> callback registered
    ///     by the middleware so a test can invoke it deterministically (<see cref="DefaultHttpContext" /> never
    ///     fires these on its own), while sharing the same <see cref="IHeaderDictionary" /> as the response.
    /// </summary>
    private sealed class CapturingResponseFeature(IHeaderDictionary headers) : IHttpResponseFeature
    {
        private object? _onStartingState;
        public Func<object, Task>? OnStartingCallback { get; private set; }

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = headers;
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            OnStartingCallback = callback;
            _onStartingState = state;
        }

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public Task FireOnStartingAsync() =>
            OnStartingCallback is null ? Task.CompletedTask : OnStartingCallback(_onStartingState!);
    }
}