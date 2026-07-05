using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.Support;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     <see cref="ZendeskKiotaRequests.SendForJsonWithStatusAsync" />: the status-capturing variant behind the
///     upsert tools' <c>created: true|false</c> signal. It must surface the status code the adapter normally
///     discards while keeping the error semantics identical to every other call path (typed
///     <see cref="ZendeskApiException" /> with the bounded body prefix and the <c>Retry-After</c> hint).
/// </summary>
public class ZendeskKiotaRequestsStatusTests
{
    [Fact]
    public async Task Surfaces_A_201_Created_With_The_Parsed_Body()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":135,"email":"sam@example.org"}}""", HttpStatusCode.Created);
        var adapter = harness.CreateAdapter(true);
        var request = new ZendeskSupportApiClient(adapter).Api.V2.Users.ToGetRequestInformation();

        var (statusCode, body) = await adapter.SendForJsonWithStatusAsync(request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, statusCode);
        Assert.Equal(135, body.GetProperty("user").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Surfaces_A_200_Ok_Distinct_From_Created()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":135}}""");
        var adapter = harness.CreateAdapter(true);
        var request = new ZendeskSupportApiClient(adapter).Api.V2.Users.ToGetRequestInformation();

        var (statusCode, body) = await adapter.SendForJsonWithStatusAsync(request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, statusCode);
        Assert.Equal(135, body.GetProperty("user").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Returns_An_Undefined_Body_For_No_Content()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.NoContent);
        var adapter = harness.CreateAdapter(true);
        var request = new ZendeskSupportApiClient(adapter).Api.V2.Users.ToGetRequestInformation();

        var (statusCode, body) = await adapter.SendForJsonWithStatusAsync(request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, statusCode);
        Assert.Equal(JsonValueKind.Undefined, body.ValueKind);
    }

    [Fact]
    public async Task Guards_A_RetryExhausted_429_With_The_Same_Typed_Error_Semantics()
    {
        // 408/429/5xx pass the response-guard HANDLER untouched (the resilience pipeline owns them), so with
        // a native response handler they reach this helper as raw responses — it must apply the same guard:
        // typed exception, bounded body, Retry-After.
        var harness = new ZendeskToolHarness();
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""{"error":"RateLimited"}""", Encoding.UTF8, "application/json")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
        harness.Enqueue(response);
        var adapter = harness.CreateAdapter(true);
        var request = new ZendeskSupportApiClient(adapter).Api.V2.Users.ToGetRequestInformation();

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            adapter.SendForJsonWithStatusAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal("""{"error":"RateLimited"}""", exception.ResponseBody);
        Assert.Equal(TimeSpan.FromSeconds(42), exception.RetryAfter);
    }

    [Fact]
    public async Task NonRetryable_Failures_Still_Throw_Typed_From_The_Guard_Handler()
    {
        // A 422 is translated by ZendeskResponseGuardHandler INSIDE the HTTP pipeline, before the native
        // response handler sees anything — proving both failure paths produce the identical exception type.
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.UnprocessableEntity, """{"error":"RecordInvalid"}""");
        var adapter = harness.CreateAdapter(true);
        var request = new ZendeskSupportApiClient(adapter).Api.V2.Users.ToGetRequestInformation();

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            adapter.SendForJsonWithStatusAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Equal("""{"error":"RecordInvalid"}""", exception.ResponseBody);
    }
}