using ES.FX.Ignite.Microsoft.EntityFrameworkCore.HealthChecks;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests.Context;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Migrations.Abstractions;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HealthChecks(bool useFactory)
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
        Assert.True(await context.Database.CanConnectAsync());

        var migrationsHealthChecks = new RelationalDbContextMigrationsHealthCheck<TestDbContext>(context);
        var healthCheckContext = new HealthCheckContext();

        var healthCheckResult = await migrationsHealthChecks.CheckHealthAsync(healthCheckContext);
        Assert.True(healthCheckResult.Status == HealthStatus.Unhealthy);

        await context.Database.MigrateAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

        healthCheckResult = await migrationsHealthChecks.CheckHealthAsync(healthCheckContext);
        Assert.True(healthCheckResult.Status == HealthStatus.Healthy);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelationalDbContextMigrationsTask(bool useFactory)
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

        builder.AddDbContextMigrationsTask<TestDbContext>();

        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();
        var migrationTask = app.Services.GetRequiredService<IMigrationsTask>();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.NotEmpty(pendingMigrations);

        await migrationTask.ApplyMigrations();
        pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        Assert.Empty(pendingMigrations);

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