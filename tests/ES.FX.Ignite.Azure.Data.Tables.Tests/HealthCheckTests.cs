using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using ES.FX.Ignite.Azure.Data.Tables.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Moq;

namespace ES.FX.Ignite.Azure.Data.Tables.Tests;

/// <summary>
///     Exercises the (internal) <c>SimpleTableServiceHealthCheck</c> end-to-end through the real
///     <see cref="AzureDataTablesHostingExtensions.IgniteAzureTableServiceClient" /> registration path.
///     The spark registers the client via <c>AddAzureClients</c> from a connection string; we then
///     override the keyed <see cref="TableServiceClient" /> registration with a Moq stand-in so the
///     healthy / empty / failure branches can be confirmed deterministically without Docker or an
///     emulator. The registered health-check factory still constructs the real internal health check.
/// </summary>
public class HealthCheckTests
{
    private static AsyncPageable<TableItem> MakePage(params TableItem[] items) =>
        AsyncPageable<TableItem>.FromPages([Page<TableItem>.FromValues(items, null, Mock.Of<Response>())]);

    private static IHost BuildHostWithMockClient(
        Mock<TableServiceClient> clientMock,
        string? serviceKey = null,
        HealthStatus? failureStatus = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureDataTablesSpark.ConfigurationSectionPath}:ConnectionString",
                "UseDevelopmentStorage=true;")
        ]);

        builder.IgniteAzureTableServiceClient(serviceKey: serviceKey, configureSettings: settings =>
        {
            if (failureStatus.HasValue) settings.HealthChecks.FailureStatus = failureStatus;
        });

        // Replace the real client registered by AddAzureClients with the mock so the internal
        // health check (resolved via GetRequiredKeyedService<TableServiceClient>) uses it.
        builder.Services.AddKeyedSingleton(serviceKey, clientMock.Object);

        return builder.Build();
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenQuerySucceeds()
    {
        var clientMock = new Mock<TableServiceClient>();
        clientMock
            .Setup(c => c.QueryAsync((string?)null, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(MakePage(new TableItem("table-a"), new TableItem("table-b")));

        using var host = BuildHostWithMockClient(clientMock);
        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenServiceHasNoTables()
    {
        // Empty enumeration: the `break` never fires, the foreach completes, still Healthy.
        var clientMock = new Mock<TableServiceClient>();
        clientMock
            .Setup(c => c.QueryAsync((string?)null, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(MakePage());

        using var host = BuildHostWithMockClient(clientMock);
        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_AndAttachesException_WhenQueryThrows()
    {
        var boom = new RequestFailedException("bad connection string");
        var clientMock = new Mock<TableServiceClient>();
        clientMock
            .Setup(c => c.QueryAsync((string?)null, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Throws(boom);

        // Default FailureStatus (null) means the health check reports Unhealthy.
        using var host = BuildHostWithMockClient(clientMock);
        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(HealthStatus.Unhealthy, entry.Value.Status);
        Assert.Same(boom, entry.Value.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesConfiguredFailureStatus_WhenQueryThrows()
    {
        var boom = new RequestFailedException("unreachable");
        var clientMock = new Mock<TableServiceClient>();
        clientMock
            .Setup(c => c.QueryAsync((string?)null, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Throws(boom);

        // FailureStatus flows through HealthCheckRegistration into context.Registration.FailureStatus.
        using var host = BuildHostWithMockClient(clientMock, failureStatus: HealthStatus.Degraded);
        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(HealthStatus.Degraded, entry.Value.Status);
        Assert.Same(boom, entry.Value.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WorksWithKeyedRegistration()
    {
        var clientMock = new Mock<TableServiceClient>();
        clientMock
            .Setup(c => c.QueryAsync((string?)null, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(MakePage(new TableItem("keyed-table")));

        using var host = BuildHostWithMockClient(clientMock, "keyed");
        var report = await host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(report.Entries);
        Assert.Equal(HealthStatus.Healthy, entry.Value.Status);
        // The health-check name carries the service key: "Azure-TableServiceClient-[keyed]".
        Assert.Contains("[keyed]", entry.Key);
    }
}