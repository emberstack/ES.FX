using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using ES.FX.NousResearch.HermesAgent.Server;
using ES.FX.NousResearch.HermesAgent.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests.Instrumentation;

/// <summary>
///     Behavioral coverage of the client's <see cref="Activity" /> emission
///     (<see cref="HermesAgentClientInstrumentation" />): one Client-kind span per operation named
///     <c>HermesAgent.{Area}.{Operation}</c> with the <c>hermesagent.operation</c> tag, <c>Ok</c> on success
///     and <c>Error</c> (with the exception attached) on failure.
/// </summary>
public class HermesAgentClientInstrumentationTests
{
    private static HermesAgentServerApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8642/") },
            NullLogger<HermesAgentServerApi>.Instance);

    /// <summary>
    ///     Subscribes to the client's <see cref="ActivitySource" /> and collects stopped activities. The source
    ///     is process-global and other tests may emit on it concurrently, so callers isolate their own spans by
    ///     filtering on a parent activity they start themselves.
    /// </summary>
    private static ActivityListener Subscribe(ConcurrentQueue<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HermesAgentClientInstrumentation.ActivitySourceName,
            Sample = (ref _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public void ActivitySourceName_Is_Pinned_To_The_Documented_Value()
    {
        // OpenTelemetry subscriptions (the Ignite Spark, user AddSource calls) reference this exact string —
        // a rename is a silent tracing outage, so pin the literal.
        Assert.Equal("ES.FX.NousResearch.HermesAgent", HermesAgentClientInstrumentation.ActivitySourceName);
    }

    [Fact]
    public async Task Successful_Operation_Emits_One_Client_Activity_With_Operation_Tag_And_Ok_Status()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = Subscribe(stopped);
        using var parent = new Activity("test-parent").Start();

        var api = CreateApi(new StubHttpMessageHandler("""{ "status": "healthy" }"""));
        await api.GetHealthAsync(TestContext.Current.CancellationToken);

        var activity = Assert.Single(stopped, a => a.ParentId == parent.Id);
        Assert.Equal("HermesAgent.Server.GetHealth", activity.OperationName); // HermesAgent.{Area}.{Operation}
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("HermesAgent.Server.GetHealth", activity.GetTagItem("hermesagent.operation"));
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public async Task Failing_Operation_Sets_Error_Status_And_Attaches_The_Exception()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = Subscribe(stopped);
        using var parent = new Activity("test-parent").Start();

        var api = CreateApi(new StubHttpMessageHandler("""{ "error": "boom" }""",
            HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<HermesAgentApiException>(async () =>
            await api.GetHealthAsync(TestContext.Current.CancellationToken));

        var activity = Assert.Single(stopped, a => a.ParentId == parent.Id);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.False(string.IsNullOrEmpty(activity.StatusDescription));
        Assert.Contains(activity.Events, e => e.Name == "exception"); // Activity.AddException event
    }
}