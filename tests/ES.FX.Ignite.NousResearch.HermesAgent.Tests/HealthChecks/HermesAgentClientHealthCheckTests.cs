using System.Net;
using System.Text;
using ES.FX.Ignite.NousResearch.HermesAgent.HealthChecks;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ES.FX.Ignite.NousResearch.HermesAgent.Tests.HealthChecks;

public class HermesAgentClientHealthCheckTests
{
    private const string CapabilitiesJson =
        """
        {
          "object": "hermes.api_server.capabilities",
          "platform": "hermes-agent",
          "model": "hermes-4-405b",
          "auth": { "type": "bearer", "required": true }
        }
        """;

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => handler));
        services.AddHermesAgentClient(configureOptions: options =>
        {
            options.BaseUrl = "http://localhost:8642";
            options.ApiKey = "test-key";
        });
        return services.BuildServiceProvider();
    }

    private static HealthCheckContext CreateContext(IHealthCheck healthCheck, HealthStatus failureStatus) => new()
    {
        // A NON-default failure status, so a regression that hardcodes Unhealthy fails the assertion.
        Registration = new HealthCheckRegistration("HermesAgentClient", healthCheck, failureStatus, null)
    };

    [Fact]
    public async Task CheckHealthAsync_Calls_The_Authenticated_Capabilities_Endpoint_And_Reports_Healthy()
    {
        var stub = new StubHandler(CapabilitiesJson);
        await using var provider = BuildProvider(stub);
        var healthCheck = new HermesAgentClientHealthCheck(provider.GetRequiredService<IHermesAgentClient>());

        var result = await healthCheck.CheckHealthAsync(CreateContext(healthCheck, HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        // /v1/capabilities is the AUTHENTICATED endpoint — /v1/health would not verify the API key.
        Assert.Equal("/v1/capabilities", stub.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer", stub.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", stub.LastRequest?.Headers.Authorization?.Parameter);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("hermes-agent", result.Description);
        Assert.Contains("hermes-4-405b", result.Description);
        // Only non-sensitive identity data may surface — the description can end up on a public /health page.
        Assert.DoesNotContain("test-key", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_On_Failure_Returns_The_Registration_FailureStatus_With_The_Exception()
    {
        var stub = new StubHandler("""{ "error": { "message": "boom" } }""", HttpStatusCode.InternalServerError);
        await using var provider = BuildProvider(stub);
        var healthCheck = new HermesAgentClientHealthCheck(provider.GetRequiredService<IHermesAgentClient>());

        var result = await healthCheck.CheckHealthAsync(CreateContext(healthCheck, HealthStatus.Degraded),
            TestContext.Current.CancellationToken);

        // Must honor context.Registration.FailureStatus, not hardcode Unhealthy.
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }

    /// <summary>Returns a canned JSON response and records the last request for assertions.</summary>
    private sealed class StubHandler(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
