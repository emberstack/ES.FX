using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Container-backed coverage of the registered health check end-to-end. Resolves the real
///     <see cref="HealthCheckService" /> and asserts it reports <see cref="HealthStatus.Healthy" />
///     against a live SQL Server (killing "always return the failure status" / "break the probe query"
///     mutations from the positive side) and the configured failure status against a dead server
///     (killing "always return Healthy" from the negative side).
/// </summary>
public class HealthCheckFunctionalTests(SqlServerContainerFixture sqlServerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task HealthCheck_Reports_Healthy_Against_Live_Server()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: s => s.HealthChecks.Enabled = true,
            configureOptions: o => o.ConnectionString = sqlServerFixture.GetConnectionString());

        var app = builder.Build();

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Contains(SqlServerClientSpark.Name, report.Entries);
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task HealthCheck_Reports_FailureStatus_Against_Dead_Server()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureSettings: s =>
            {
                s.HealthChecks.Enabled = true;
                s.HealthChecks.FailureStatus = HealthStatus.Degraded;
            },
            // Unreachable server with a short connect timeout.
            configureOptions: o => o.ConnectionString =
                "Server=127.0.0.1,1;Database=x;Connect Timeout=2;Encrypt=False;TrustServerCertificate=True");

        var app = builder.Build();

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Contains(SqlServerClientSpark.Name, report.Entries);
        Assert.Equal(HealthStatus.Degraded, entry.Status);
        Assert.NotEqual(HealthStatus.Healthy, entry.Status);
    }
}
