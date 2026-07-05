namespace ES.FX.Zendesk.Tests.Instrumentation;

/// <summary>
///     Pins the instrumentation constants of <see cref="ZendeskClientInstrumentation" /> to their documented
///     values. OpenTelemetry subscriptions (the Ignite Spark, user <c>AddSource</c> calls) reference these exact
///     strings — a rename is a silent tracing outage, so pin the literals.
/// </summary>
public class ZendeskClientInstrumentationTests
{
    [Fact]
    public void ActivitySourceName_Is_Pinned_To_The_Documented_Value()
    {
        Assert.Equal("ES.FX.Zendesk", ZendeskClientInstrumentation.ActivitySourceName);
    }

    [Fact]
    public void KiotaActivitySourceName_Is_Pinned_To_The_Kiota_Http_Library_Source()
    {
        // The Kiota request adapter emits request spans on its own fixed ActivitySource — the name is not
        // configurable in the Kiota HTTP library, so subscribers must use this exact string to see request spans.
        Assert.Equal("Microsoft.Kiota.Http.HttpClientLibrary",
            ZendeskClientInstrumentation.KiotaActivitySourceName);
    }

    [Fact]
    public void The_Shared_ActivitySource_Uses_The_Documented_Name()
    {
        Assert.Equal(ZendeskClientInstrumentation.ActivitySourceName,
            ZendeskClientInstrumentation.ActivitySource.Name);
    }
}