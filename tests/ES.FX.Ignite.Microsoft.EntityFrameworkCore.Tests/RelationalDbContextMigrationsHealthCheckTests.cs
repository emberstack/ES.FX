using ES.FX.Ignite.Microsoft.EntityFrameworkCore.HealthChecks;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests;

public class RelationalDbContextMigrationsHealthCheckTests
{
    /// <summary>
    ///     Builds a <see cref="TestDbContext" /> backed by the in-memory provider. That provider does not
    ///     support relational migration APIs, so <c>GetPendingMigrationsAsync</c> throws — which is exactly
    ///     what drives the health check's catch branch deterministically (no Docker needed).
    /// </summary>
    private static TestDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HealthCheckContext CreateContextWithRegistration(HealthStatus failureStatus) =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "migrations",
                Mock.Of<IHealthCheck>(),
                failureStatus,
                tags: null)
        };

    [Fact]
    public async Task CheckHealthAsync_NullContext_Throws()
    {
        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsHealthCheck<TestDbContext>(context);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => sut.CheckHealthAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPendingMigrationsQueryThrows_ReturnsUnhealthyWithException()
    {
        // In-memory provider makes GetPendingMigrationsAsync throw -> catch branch.
        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsHealthCheck<TestDbContext>(context);

        var result = await sut.CheckHealthAsync(
            CreateContextWithRegistration(HealthStatus.Unhealthy), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ExceptionBranch_HonorsRegistrationFailureStatus()
    {
        // The failure status must come from the registration, not a hard-coded Unhealthy.
        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsHealthCheck<TestDbContext>(context);

        var result = await sut.CheckHealthAsync(
            CreateContextWithRegistration(HealthStatus.Degraded), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_ExceptionBranch_NullRegistration_FallsBackToUnhealthy()
    {
        // Registration is unset when invoked directly (outside the health check service):
        // the failureStatus fallback must be Unhealthy.
        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsHealthCheck<TestDbContext>(context);

        var result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
