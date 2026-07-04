using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Fast, deterministic, network-free coverage of the internal <c>SimpleSqlServerHealthCheck</c>
///     failure path: when the probe cannot complete, the check must map the error to the
///     registration's <see cref="HealthCheckRegistration.FailureStatus" /> and capture the exception,
///     rather than swallowing it and returning <see cref="HealthStatus.Healthy" />.
///     The healthy path (which requires a reachable server) is covered by the container-backed
///     <see cref="HealthCheckFunctionalTests" />.
/// </summary>
public class SimpleSqlServerHealthCheckTests
{
    // SimpleSqlServerHealthCheck is internal; construct it via reflection so the test project does
    // not need InternalsVisibleTo.
    private static IHealthCheck CreateHealthCheck(string connectionString)
    {
        var type = typeof(SqlServerClientHostingExtensions).Assembly.GetType(
            "ES.FX.Ignite.Microsoft.Data.SqlClient.HealthChecks.SimpleSqlServerHealthCheck", true)!;
        return (IHealthCheck)Activator.CreateInstance(type, connectionString)!;
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "SqlServerClient",
                Mock.Of<IHealthCheck>(),
                failureStatus,
                null)
        };

    [Theory]
    [InlineData(HealthStatus.Unhealthy)]
    [InlineData(HealthStatus.Degraded)]
    public async Task Maps_Probe_Failure_To_Registration_FailureStatus(HealthStatus failureStatus)
    {
        // A syntactically valid but unreachable target with a 1s connect timeout: OpenAsync throws,
        // which the catch block must map to the registration FailureStatus.
        const string connectionString =
            "Server=127.0.0.1,1;Database=x;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True";
        var sut = CreateHealthCheck(connectionString);

        var result = await sut.CheckHealthAsync(
            CreateContext(failureStatus), TestContext.Current.CancellationToken);

        // If the catch block returned Healthy() (surviving mutation) this would fail.
        Assert.Equal(failureStatus, result.Status);
        Assert.NotEqual(HealthStatus.Healthy, result.Status);
        // The captured exception proves the probe was actually attempted and failed.
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task Maps_Cancellation_To_FailureStatus_Without_Returning_Healthy()
    {
        // A never-completing connect; cancellation makes OpenAsync throw OperationCanceledException,
        // which the catch block maps to the failure status. Fully deterministic, no live server.
        const string connectionString =
            "Server=127.0.0.1,1;Database=x;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True";
        var sut = CreateHealthCheck(connectionString);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await sut.CheckHealthAsync(CreateContext(HealthStatus.Unhealthy), cts.Token);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotEqual(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}