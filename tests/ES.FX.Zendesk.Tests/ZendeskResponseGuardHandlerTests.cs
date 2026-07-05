using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ES.FX.Zendesk.Tests.Testing;

namespace ES.FX.Zendesk.Tests;

/// <summary>
///     Behavioral coverage of the innermost handler that turns non-success responses into typed
///     <see cref="ZendeskApiException" />s — EXCEPT the statuses the standard resilience pipeline retries
///     (<c>408</c>, <c>429</c>, <c>5xx</c>), which must pass through untouched or the guard would defeat the
///     retry policy it sits inside of.
/// </summary>
public class ZendeskResponseGuardHandlerTests
{
    private static HttpMessageInvoker CreateInvoker(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new ZendeskResponseGuardHandler { InnerHandler = new CountingHandler(responder) });

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Get, "https://acme.zendesk.com/api/v2/tickets/1");

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task NonRetryable_Failure_Throws_ZendeskApiException_With_Status_And_Body(HttpStatusCode status)
    {
        using var invoker = CreateInvoker(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"error":"RecordInvalid","description":"boom"}""",
                Encoding.UTF8, "application/json")
        });
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await invoker.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(status, exception.StatusCode);
        Assert.Contains("RecordInvalid", exception.ResponseBody); // the body prefix survives the Kiota adapter
    }

    [Fact]
    public async Task NonRetryable_Failure_Surfaces_The_RetryAfter_Hint()
    {
        using var invoker = CreateInvoker(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("slow down")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });
        using var request = CreateRequest();

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await invoker.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)] // 408
    [InlineData(HttpStatusCode.TooManyRequests)] // 429
    [InlineData(HttpStatusCode.InternalServerError)] // 500
    [InlineData(HttpStatusCode.ServiceUnavailable)] // 503
    public async Task Retryable_Statuses_Pass_Through_Untouched_For_The_Resilience_Layer(HttpStatusCode status)
    {
        // The guard sits INSIDE the resilience handler: throwing here would defeat the retry policy, so these
        // statuses must come back as the original response — same instance, body unconsumed.
        var canned = new HttpResponseMessage(status)
        {
            Content = new StringContent("""{"error":"try again later"}""", Encoding.UTF8, "application/json")
        };
        using var invoker = CreateInvoker(_ => canned);
        using var request = CreateRequest();

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(canned, response);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("""{"error":"try again later"}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Success_Passes_Through_Untouched()
    {
        var canned = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ticket":{"id":1}}""", Encoding.UTF8, "application/json")
        };
        using var invoker = CreateInvoker(_ => canned);
        using var request = CreateRequest();

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(canned, response);
        Assert.Equal("""{"ticket":{"id":1}}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}