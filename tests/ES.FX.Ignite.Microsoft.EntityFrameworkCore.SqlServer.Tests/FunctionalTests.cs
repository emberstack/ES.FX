using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

public class FunctionalTests(SqlServerContainerFixture sqlServerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanConnect(bool useFactory)
    {
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
        Assert.True(await context.Database.CanConnectAsync());

        await context.Database.MigrateAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
            context = await factory.CreateDbContextAsync();
            Assert.True(await context.Database.CanConnectAsync());

            await context.Database.MigrateAsync();
            pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            Assert.Empty(pendingMigrations);
        }

        return;

        void ConfigureOptions(SqlServerDbContextSparkOptions<TestDbContext> options)
        {
            options.ConnectionString = sqlServerFixture.GetConnectionString();
        }

        void ConfigureSqlServerDbContextOptionsBuilder(
            SqlServerDbContextOptionsBuilder sqlServerDbContextOptionsBuilder)
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                typeof(TestDbContextDesignTimeFactory).Assembly.FullName);
        }
    }
}