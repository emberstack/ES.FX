using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Spark;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Ignite.Spark.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

#pragma warning disable EF1001 // Internal EF Core API usage.
public class SqlServerDbContextHostingExtensionsTests
{
    [Fact]
    public void AddSqlServerDbContext_CanResolveDbContext()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContext<TestDbContext>();

        var app = builder.Build();

        app.Services.GetRequiredService<TestDbContext>();
    }

    [Fact]
    public void AddSqlServerDbContextFactory_CanResolveFactoryAndDbContext()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContextFactory<TestDbContext>();

        var app = builder.Build();

        app.Services.GetRequiredService<TestDbContext>();
        app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
    }

    [Fact]
    public void AddSqlServerDbContextFactory_Lifetime_Transient_FactoryHasCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContextFactory<TestDbContext>(lifetime: ServiceLifetime.Transient);

        var app = builder.Build();

        var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        Assert.NotSame(factory1, factory2);
    }

    [Fact]
    public void AddSqlServerDbContextFactory_Lifetime_Singleton_FactoryHasCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContextFactory<TestDbContext>(lifetime: ServiceLifetime.Singleton);

        var app = builder.Build();

        var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void AddSqlServerDbContextFactory_Lifetime_Scoped_FactoryAndContextHaveCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContextFactory<TestDbContext>(lifetime: ServiceLifetime.Scoped);

        var app = builder.Build();

        // Scoped factories should be the same within the same scope
        var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
        Assert.Same(factory1, factory2);

        // Factory created DbContexts should not be scoped if the factory is scoped
        var createdDbContext1 = factory1.CreateDbContext();
        var createdDbContext2 = factory1.CreateDbContext();
        Assert.NotSame(createdDbContext1, createdDbContext2);

        // Resolved DbContexts should be the same within the same scope
        var resolvedDbContext1 = app.Services.GetRequiredService<TestDbContext>();
        var resolvedDbContext2 = app.Services.GetRequiredService<TestDbContext>();
        Assert.Same(resolvedDbContext1, resolvedDbContext2);


        // Factories should be different in different scopes
        var scope = app.Services.CreateScope();
        var scopedFactory1 = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
        var scopedFactory2 = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
        Assert.Same(scopedFactory1, scopedFactory2);
        Assert.NotSame(scopedFactory1, factory1);

        // Scope resolved DbContexts should be the same within the same scope
        var scopedDbContext1 = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var scopedDbContext2 = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        Assert.Same(scopedDbContext1, scopedDbContext2);
        Assert.NotSame(scopedDbContext1, resolvedDbContext1);
    }


    [Fact]
    public void AddSqlServerDbContext_CanChangeSettingsInCode()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure settings
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.TracingEnabled)}",
                true.ToString()),
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.HealthChecksEnabled)}",
                true.ToString())
        ]);
        builder.AddSqlServerDbContext<TestDbContext>(
            configureSettings: settings =>
            {
                //Settings should have correct value from configuration
                Assert.True(settings.TracingEnabled);
                Assert.True(settings.HealthChecksEnabled);

                //Change the settings
                settings.TracingEnabled = false;
            });

        var app = builder.Build();

        var settings = app.Services.GetRequiredService<SqlServerDbContextSparkSettings<TestDbContext>>();
        Assert.False(settings.TracingEnabled);
        Assert.True(settings.HealthChecksEnabled);
    }

    [Fact]
    public void AddSqlServerDbContext_CanChangeOptionsInCode()
    {
        var initialConnectionString = new SqlConnectionStringBuilder
        {
            InitialCatalog = "InitialDatabase",
            DataSource = "InitialServer"
        }.ConnectionString;
        const int initialCommandTimeout = 123;

        var changedConnectionString = new SqlConnectionStringBuilder(initialConnectionString)
        {
            InitialCatalog = "ChangedDatabase"
        }.ConnectionString;
        const int changedCommandTimeout = 500;


        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure options
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{nameof(SqlServerDbContextSparkOptions<TestDbContext>.ConnectionString)}",
                initialConnectionString),
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{nameof(SqlServerDbContextSparkOptions<TestDbContext>.CommandTimeout)}",
                initialCommandTimeout.ToString())
        ]);
        builder.AddSqlServerDbContext<TestDbContext>(
            configureOptions: options =>
            {
                //Options should have correct value from configuration
                Assert.Equal(initialConnectionString, options.ConnectionString);
                Assert.Equal(initialCommandTimeout, options.CommandTimeout);

                //Change the options
                options.ConnectionString = changedConnectionString;
                options.CommandTimeout = changedCommandTimeout;
            });

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<SqlServerDbContextSparkOptions<TestDbContext>>>();
        Assert.Equal(changedConnectionString, options.Value.ConnectionString);
        Assert.Equal(changedCommandTimeout, options.Value.CommandTimeout);


        var context = app.Services.GetRequiredService<TestDbContext>();

        Assert.Equal(changedCommandTimeout, context.Options.FindExtension<SqlServerOptionsExtension>()?.CommandTimeout);
        Assert.Equal(changedConnectionString,
            context.Options.FindExtension<SqlServerOptionsExtension>()?.ConnectionString);
    }

    [Fact]
    public void AddSqlServerDbContext_CanChangeDbContextOptionsInCode()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContext<TestDbContext>(
            configureDbContextOptionsBuilder: dbContextOptionsBuilder =>
            {
                //Enable sensitive data logging
                dbContextOptionsBuilder.EnableSensitiveDataLogging();
            });

        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();

        //Check if sensitive data logging is enabled
        Assert.True(context.Options.FindExtension<CoreOptionsExtension>()?.IsSensitiveDataLoggingEnabled);
    }


    [Fact]
    public void AddSqlServerDbContext_CanChangeSqlServerDbContextOptionsInCode()
    {
        const int commandTimeout = 12345;

        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerDbContext<TestDbContext>(
            configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
            {
                sqlServerDbContextOptionsBuilder.CommandTimeout(commandTimeout);
            });

        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();

        Assert.Equal(commandTimeout, context.Database.GetCommandTimeout());
    }
}