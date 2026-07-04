using Azure;
using Azure.Security.KeyVault.Secrets;
using ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Configuration;
using ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Moq;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Tests;

/// <summary>
///     Exercises the <c>SimpleKeyVaultSecretsHealthCheck</c> branches end-to-end through the
///     registered <see cref="HealthCheckService" />. The health check itself is internal, so it is
///     verified via the real Ignite wiring: a mocked <see cref="SecretClient" /> is registered so the
///     health check factory resolves it, then the <see cref="HealthCheckService" /> is run and the
///     resulting <see cref="HealthReportEntry" /> asserted.
/// </summary>
public class HealthCheckTests
{
    private const string ServiceKey = "keyed";

    // The health check name produced by IgniteAzureClientObservability for a keyed SecretClient.
    private const string HealthCheckName = "Azure-SecretClient-[keyed]";

    private static async Task<HealthReportEntry> RunHealthCheckAsync(
        Mock<SecretClient> mockClient,
        Action<AzureKeyVaultSecretsSparkSettings>? configureSettings = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureKeyVaultSecretsSpark.ConfigurationSectionPath}:VaultUri", "https://vaulturi")
        ]);

        builder.IgniteAzureKeyVaultSecretClient(serviceKey: ServiceKey, configureSettings: configureSettings);

        // Register the mocked client LAST so GetRequiredKeyedService<SecretClient>(ServiceKey) — used by the
        // health check factory — resolves the mock instead of the real Azure client.
        builder.Services.AddKeyedSingleton(ServiceKey, mockClient.Object);

        var app = builder.Build();

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(registration => registration.Name == HealthCheckName);

        True(report.Entries.ContainsKey(HealthCheckName),
            $"Expected a health check named '{HealthCheckName}' to be registered.");
        return report.Entries[HealthCheckName];
    }

    // SecretClient exposes two GetSecretAsync virtual overloads (a 3-arg and a 4-arg with outContentType).
    // Overload resolution of the health check's call is an implementation detail, so we configure BOTH so the
    // test is robust regardless of which the library binds to.
    private static Mock<SecretClient> CreateSucceedingClient()
    {
        // SecretClient has a protected parameterless ctor; Moq can subclass it and override the virtual methods.
        var mock = new Mock<SecretClient>();
        var secret = new KeyVaultSecret("AzureKeyVaultSecretsHealthCheck", "value");
        var response = Response.FromValue(secret, Mock.Of<Response>());
        mock.Setup(c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        mock.Setup(c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return mock;
    }

    private static Mock<SecretClient> CreateThrowingClient(Exception exception)
    {
        var mock = new Mock<SecretClient>();
        mock.Setup(c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        mock.Setup(c => c.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return mock;
    }

    [Fact]
    public async Task CheckHealth_WhenGetSecretSucceeds_ReturnsHealthy()
    {
        var mock = CreateSucceedingClient();

        var entry = await RunHealthCheckAsync(mock);

        Equal(HealthStatus.Healthy, entry.Status);
        Null(entry.Exception);
    }

    [Fact]
    public async Task CheckHealth_WhenGetSecretThrows404_ReturnsHealthy()
    {
        // A 404 means the vault connection succeeded but the sentinel secret does not exist — treated as Healthy.
        var mock = CreateThrowingClient(new RequestFailedException(404, "SecretNotFound"));

        var entry = await RunHealthCheckAsync(mock);

        Equal(HealthStatus.Healthy, entry.Status);
        Null(entry.Exception);
    }

    [Fact]
    public async Task CheckHealth_WhenGetSecretThrowsNon404RequestFailed_ReturnsUnhealthyWithException()
    {
        // A 403 (or any non-404) means the probe genuinely failed — must surface as the failure status.
        var thrown = new RequestFailedException(403, "Forbidden");
        var mock = CreateThrowingClient(thrown);

        var entry = await RunHealthCheckAsync(mock);

        // Default FailureStatus is Unhealthy.
        Equal(HealthStatus.Unhealthy, entry.Status);
        Same(thrown, entry.Exception);
    }

    [Fact]
    public async Task CheckHealth_WhenGetSecretThrowsGenericException_ReturnsUnhealthyWithException()
    {
        var thrown = new InvalidOperationException("boom");
        var mock = CreateThrowingClient(thrown);

        var entry = await RunHealthCheckAsync(mock);

        Equal(HealthStatus.Unhealthy, entry.Status);
        Same(thrown, entry.Exception);
    }

    [Fact]
    public async Task CheckHealth_HonorsConfiguredFailureStatus()
    {
        // Registration.FailureStatus flows from settings.HealthChecks.FailureStatus — assert the non-404
        // branch maps to the configured status (Degraded), not the default Unhealthy.
        var mock = CreateThrowingClient(new RequestFailedException(500, "ServerError"));

        var entry = await RunHealthCheckAsync(mock,
            settings => settings.HealthChecks.FailureStatus = HealthStatus.Degraded);

        Equal(HealthStatus.Degraded, entry.Status);
    }
}