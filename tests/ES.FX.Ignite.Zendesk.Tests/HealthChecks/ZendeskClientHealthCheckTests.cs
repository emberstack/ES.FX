using System.Net;
using System.Text;
using ES.FX.Ignite.Zendesk.HealthChecks;
using ES.FX.Zendesk.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.Zendesk.Tests.HealthChecks;

public class ZendeskClientHealthCheckTests
{
    private const string AccessToken = "stub-access-token";

    private const string TokenJson =
        $$"""
          { "access_token": "{{AccessToken}}", "token_type": "bearer", "expires_in": 3600 }
          """;

    private const string CurrentUserJson =
        """
        {
          "user": {
            "id": 12345,
            "name": "Support Bot",
            "email": "bot@unit-test.example",
            "role": "admin"
          }
        }
        """;

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        // Every factory-created client — including the dedicated token client — gets the stub as its
        // primary handler, so no request in these tests can reach a real Zendesk host.
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        services.AddZendeskClient(options =>
        {
            options.Subdomain = "unit-test-offline";
            options.OAuth.ClientId = "cid";
            options.OAuth.ClientSecret = "unit-test-client-secret";
        });
        return services.BuildServiceProvider();
    }

    private static HealthCheckContext CreateContext(IHealthCheck healthCheck, HealthStatus failureStatus) => new()
    {
        // A NON-default failure status, so a regression that hardcodes Unhealthy fails the assertion.
        Registration = new HealthCheckRegistration("ZendeskClient", healthCheck, failureStatus, null)
    };

    [Fact]
    public async Task CheckHealthAsync_Calls_The_Authenticated_Users_Me_Endpoint_And_Reports_Healthy()
    {
        var stub = new RoutingStubHandler();
        await using var provider = BuildProvider(stub);
        var healthCheck = new ZendeskClientHealthCheck(provider.GetRequiredService<IZendeskClient>());

        var result = await healthCheck.CheckHealthAsync(CreateContext(healthCheck, HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        // Exactly two outbound requests, both intercepted by the stub: the token fetch, then the probe.
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal("/oauth/tokens", stub.TokenRequest?.RequestUri?.AbsolutePath);
        // The dedicated token client has no auth handler attached, so the token request is unauthenticated.
        Assert.Null(stub.TokenRequest?.Headers.Authorization);
        Assert.Equal("/api/v2/users/me.json", stub.UsersMeRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", stub.UsersMeRequest?.Headers.Authorization?.Scheme);
        Assert.Equal(AccessToken, stub.UsersMeRequest?.Headers.Authorization?.Parameter);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("12345", result.Description);
        Assert.Contains("admin", result.Description);
        // The description can surface on an unauthenticated /health endpoint — no PII, no secrets.
        Assert.DoesNotContain("bot@unit-test.example", result.Description);
        Assert.DoesNotContain("unit-test-client-secret", result.Description);
        Assert.DoesNotContain(AccessToken, result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_On_Failure_Returns_The_Registration_FailureStatus_With_The_Exception()
    {
        var stub = new RoutingStubHandler { UsersMeStatusCode = HttpStatusCode.InternalServerError };
        await using var provider = BuildProvider(stub);
        var healthCheck = new ZendeskClientHealthCheck(provider.GetRequiredService<IZendeskClient>());

        var result = await healthCheck.CheckHealthAsync(CreateContext(healthCheck, HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        // Must honor context.Registration.FailureStatus, not hardcode Unhealthy.
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }

    /// <summary>
    ///     Serves the OAuth token endpoint and <c>users/me.json</c> with canned responses, records the requests
    ///     for assertions, and fails the test on any other outbound request.
    /// </summary>
    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public HttpRequestMessage? TokenRequest { get; private set; }

        public HttpRequestMessage? UsersMeRequest { get; private set; }

        public HttpStatusCode UsersMeStatusCode { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            switch (request.RequestUri?.AbsolutePath)
            {
                case "/oauth/tokens":
                    TokenRequest = request;
                    return Task.FromResult(Json(HttpStatusCode.OK, TokenJson));
                case "/api/v2/users/me.json":
                    UsersMeRequest = request;
                    return Task.FromResult(Json(UsersMeStatusCode,
                        UsersMeStatusCode == HttpStatusCode.OK
                            ? CurrentUserJson
                            : """{ "error": "InternalServerError" }"""));
                default:
                    throw new InvalidOperationException(
                        $"Unexpected outbound request {request.Method} {request.RequestUri} — " +
                        "health check tests must never leave the stub.");
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
