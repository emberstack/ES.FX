using ES.FX.Shared.SqlServer.Tests.Fixtures;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Tests.Context;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer.Tests;

public class OutboxDeliveryTests(SqlServerContainerFixture sqlServerFixture, ITestOutputHelper output)
    : OutboxDeliveryTestsBase(output)
{
    protected override void ConfigureDbContext(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString,
            o => o.MigrationsAssembly(typeof(OutboxDeliveryTests).Assembly.FullName));
    }

    protected override void ConfigureOutboxDelivery(OutboxDeliveryOptions<OutboxTestDbContext> options)
    {
        options.UseSqlServerOutboxProvider();
    }

    // A fresh database on the shared server per test; it is discarded along with the container.
    protected override Task<string> GetConnectionStringAsync() =>
        sqlServerFixture.CreateDatabaseAsync(TestContext.Current.CancellationToken);

    protected override async Task InitializeDatabaseAsync(OutboxTestDbContext context)
    {
        // SQL Server uses migrations
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
}
