using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Vies.Tests;

public class ViesClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IViesClient Client, StubHttpMessageHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var services = new ServiceCollection();
        services.AddVies(
            configureHttpClient: b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IViesClient>(), handler);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Valid_number_returns_valid_with_trader_details()
    {
        var (client, handler) = Build(_ => Json(
            """{"countryCode":"RO","vatNumber":"12345678","requestDate":"2025-12-01T00:00:00Z","valid":true,"name":"ACME SRL","address":"Str. Exemplu 1"}"""));

        var result = await client.ValidateAsync("ro", "12345678", Ct);

        Assert.Equal(ViesValidationStatus.Valid, result.Status);
        Assert.Equal("ACME SRL", result.Name);
        Assert.Equal("Str. Exemplu 1", result.Address);
        Assert.Equal("RO", result.CountryCode);

        // Posted to the correct endpoint with a camelCase body.
        Assert.EndsWith("check-vat-number", handler.LastRequest!.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("\"countryCode\":\"RO\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_number_returns_invalid()
    {
        var (client, _) = Build(_ => Json("""{"valid":false}"""));
        Assert.Equal(ViesValidationStatus.Invalid, (await client.ValidateAsync("RO", "0", Ct)).Status);
    }

    [Fact]
    public async Task Member_state_unavailable_is_a_value_not_an_exception()
    {
        var (client, _) = Build(_ => Json("""{"valid":false,"userError":"MS_UNAVAILABLE"}"""));
        var result = await client.ValidateAsync("RO", "12345678", Ct);
        Assert.Equal(ViesValidationStatus.MemberStateUnavailable, result.Status);
    }

    [Fact]
    public async Task Placeholder_dashes_are_cleaned_to_null()
    {
        var (client, _) = Build(_ => Json("""{"valid":true,"name":"---","address":"---"}"""));
        var result = await client.ValidateAsync("RO", "12345678", Ct);
        Assert.Null(result.Name);
        Assert.Null(result.Address);
    }

    [Fact]
    public async Task Service_fault_in_error_wrappers_throws_typed_exception()
    {
        var (client, _) = Build(_ => Json(
            """{"actionSucceed":false,"errorWrappers":[{"error":"SERVICE_UNAVAILABLE","message":"try later"}]}"""));

        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Equal("SERVICE_UNAVAILABLE", exception.FaultCode);
    }

    [Fact]
    public async Task Invalid_input_throws_argument_exception()
    {
        var (client, _) = Build(_ => Json("""{"userError":"INVALID_INPUT"}"""));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ValidateAsync("RO", "??", Ct));
    }

    [Fact]
    public async Task Non_success_status_throws_with_status_code()
    {
        var (client, _) = Build(_ => Json("upstream boom", HttpStatusCode.InternalServerError));
        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task Emits_a_client_activity_on_the_named_source()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ES.FX.OpenData.Vies",
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var (client, _) = Build(_ => Json("""{"valid":true}"""));
        await client.ValidateAsync("RO", "12345678", Ct);

        Assert.Contains(activities, a => a.DisplayName == "VIES ValidateVatNumber");
    }

    [Fact]
    public async Task Malformed_json_body_throws_typed_parse_exception()
    {
        var (client, _) = Build(_ => Json("this is not json {"));
        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Contains("could not be parsed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_body_throws_typed_exception()
    {
        // A literal JSON null deserializes to a null DTO — the "empty response body" branch.
        var (client, _) = Build(_ => Json("null"));
        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", "12345678")]
    [InlineData("   ", "12345678")]
    [InlineData("RO", "")]
    [InlineData("RO", "   ")]
    public async Task Blank_arguments_throw_argument_exception(string countryCode, string vatNumber)
    {
        // Rejected before any HTTP call, so the responder is never invoked.
        var (client, _) = Build(_ => Json("{}"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ValidateAsync(countryCode, vatNumber, Ct));
    }

    [Fact]
    public async Task Retry_after_header_is_surfaced_on_the_exception()
    {
        var (client, _) = Build(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("busy", Encoding.UTF8, "text/plain")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task Oversized_error_body_is_truncated_on_the_exception()
    {
        // The client caps captured bodies at ViesHttp.MaxResponseBodyLength (2048) chars.
        var (client, _) = Build(_ => Json(new string('x', 3000), HttpStatusCode.InternalServerError));
        var exception = await Assert.ThrowsAsync<ViesApiException>(() => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Equal(2048, exception.ResponseBody!.Length);
    }

    [Fact]
    public void AddVies_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddVies();
        services.AddVies();

        var provider = services.BuildServiceProvider();
        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("ES.FX.OpenData.Vies");

        // A second AddVies must not stack the client configuration (e.g. duplicate Accept headers).
        Assert.Single(httpClient.DefaultRequestHeaders.Accept);
    }
}