using EntityFramework.Exceptions.Common;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context.Entities;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

/// <summary>
///     Verifies that the Spark's <c>UseExceptionProcessor()</c> wiring translates raw SqlServer provider
///     errors into the strongly-typed EntityFramework.Exceptions hierarchy. Requires a real SqlServer engine
///     because the translation is driven by provider-specific error numbers, so it uses the shared
///     Testcontainers fixture like the other functional tests in this project.
/// </summary>
public class ExceptionProcessorFunctionalTests(SqlServerContainerFixture sqlServerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateKey_IsTranslatedTo_UniqueConstraintException(bool useFactory)
    {
        await sqlServerFixture.InitializeAsync();

        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(
                configureOptions: ConfigureOptions,
                configureSqlServerDbContextOptionsBuilder: ConfigureSqlServerDbContextOptionsBuilder);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(
                configureOptions: ConfigureOptions,
                configureSqlServerDbContextOptionsBuilder: ConfigureSqlServerDbContextOptionsBuilder);

        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var id = Guid.CreateVersion7();

        // First insert succeeds.
        context.TestUsers.Add(new TestUser { Id = id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Insert the same primary key again from a fresh context so the EF change tracker's identity map
        // does not short-circuit the conflict; the duplicate must reach the database to violate the PK
        // constraint. Without the exception processor this surfaces as a raw DbUpdateException wrapping a
        // SqlException; with the Spark's UseExceptionProcessor() it is mapped to the strongly-typed
        // UniqueConstraintException.
        var scope = app.Services.CreateScope();
        var secondContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        secondContext.TestUsers.Add(new TestUser { Id = id });

        await Assert.ThrowsAsync<UniqueConstraintException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        return;

        void ConfigureOptions(SqlServerDbContextSparkOptions<TestDbContext> options) =>
            options.ConnectionString = sqlServerFixture.GetConnectionString();

        void ConfigureSqlServerDbContextOptionsBuilder(
            SqlServerDbContextOptionsBuilder sqlServerDbContextOptionsBuilder) =>
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                typeof(TestDbContextDesignTimeFactory).Assembly.FullName);
    }
}