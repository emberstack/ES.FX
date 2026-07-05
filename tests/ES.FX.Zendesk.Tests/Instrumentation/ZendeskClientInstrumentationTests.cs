using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Instrumentation;

/// <summary>
///     Behavioral coverage of the client's <see cref="Activity" /> emission
///     (<see cref="ZendeskClientInstrumentation" />): one Client-kind span per operation named
///     <c>Zendesk.{Area}.{Operation}</c> with the <c>zendesk.operation</c> tag, <c>Ok</c> on success and
///     <c>Error</c> (with the exception attached) on failure.
/// </summary>
public class ZendeskClientInstrumentationTests
{
    private static ZendeskUsersApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskUsersApi>.Instance);

    /// <summary>
    ///     Subscribes to the client's <see cref="ActivitySource" /> and collects stopped activities. The source
    ///     is process-global and other tests may emit on it concurrently, so callers isolate their own spans by
    ///     filtering on a parent activity they start themselves.
    /// </summary>
    private static ActivityListener Subscribe(ConcurrentQueue<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ZendeskClientInstrumentation.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
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
        Assert.Equal("ES.FX.Zendesk", ZendeskClientInstrumentation.ActivitySourceName);
    }

    [Fact]
    public async Task Successful_Operation_Emits_One_Client_Activity_With_Operation_Tag_And_Ok_Status()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = Subscribe(stopped);
        using var parent = new Activity("test-parent").Start();

        var users = CreateApi(new StubHttpMessageHandler("""{ "user": { "id": 42, "name": "Jane" } }"""));
        await users.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        var activity = Assert.Single(stopped, a => a.ParentId == parent.Id);
        Assert.Equal("Zendesk.Users.GetCurrent", activity.OperationName); // Zendesk.{Area}.{Operation}
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("Zendesk.Users.GetCurrent", activity.GetTagItem("zendesk.operation"));
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public async Task Failing_Operation_Sets_Error_Status_And_Attaches_The_Exception()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = Subscribe(stopped);
        using var parent = new Activity("test-parent").Start();

        var users = CreateApi(new StubHttpMessageHandler("""{ "error": "boom" }""",
            HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await users.GetCurrentUserAsync(TestContext.Current.CancellationToken));

        var activity = Assert.Single(stopped, a => a.ParentId == parent.Id);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.False(string.IsNullOrEmpty(activity.StatusDescription));
        Assert.Contains(activity.Events, e => e.Name == "exception"); // Activity.AddException event
    }
}
