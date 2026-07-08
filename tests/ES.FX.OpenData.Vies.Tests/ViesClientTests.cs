using System.Diagnostics;
using System.Net;
using System.Text;
using ES.FX.OpenData;
using ES.FX.OpenData.Vies;
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
        services.AddOpenData().AddVies(
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

        var exception = await Assert.ThrowsAsync<ViesApiException>(
            () => client.ValidateAsync("RO", "12345678", Ct));
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
        var exception = await Assert.ThrowsAsync<ViesApiException>(
            () => client.ValidateAsync("RO", "12345678", Ct));
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task Emits_a_client_activity_on_the_named_source()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ES.FX.OpenData.Vies",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var (client, _) = Build(_ => Json("""{"valid":true}"""));
        await client.ValidateAsync("RO", "12345678", Ct);

        Assert.Contains(activities, a => a.DisplayName == "VIES ValidateVatNumber");
    }
}
