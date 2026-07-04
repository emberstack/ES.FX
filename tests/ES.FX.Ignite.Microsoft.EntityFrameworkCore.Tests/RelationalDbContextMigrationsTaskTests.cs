using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests;

public class RelationalDbContextMigrationsTaskTests
{
    /// <summary>
    ///     In-memory provider does not support relational migrations, so <c>GetPendingMigrationsAsync</c>
    ///     throws. This deterministically exercises the task's failure path without Docker. The
    ///     happy path (no pending / apply pending) is covered by the SqlServer functional suite.
    /// </summary>
    private static TestDbContext CreateInMemoryContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ApplyMigrations_WhenPendingMigrationsQueryThrows_Propagates()
    {
        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsTask<TestDbContext>(
            NullLogger<RelationalDbContextMigrationsTask<TestDbContext>>.Instance, context);

        // GetPendingMigrationsAsync on the in-memory provider is not supported and throws.
        await Assert.ThrowsAnyAsync<Exception>(() => sut.ApplyMigrations(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplyMigrations_LogsStartBeforeFailing()
    {
        // Confirms the "Applying migrations for {ContextType}" information log is emitted (before
        // the provider throws), proving the task begins work and logs the context type.
        var logger = new Mock<ILogger<RelationalDbContextMigrationsTask<TestDbContext>>>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        await using var context = CreateInMemoryContext();
        var sut = new RelationalDbContextMigrationsTask<TestDbContext>(logger.Object, context);

        await Assert.ThrowsAnyAsync<Exception>(() => sut.ApplyMigrations(TestContext.Current.CancellationToken));

        logger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(nameof(TestDbContext))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}