using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Configuration;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Tests.Context;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Tests;

#pragma warning disable EF1001 // Internal EF Core API usage.
public class HostingTests
{
    [Theory]
    //Transient
    [InlineData(false, ServiceLifetime.Transient)]
    [InlineData(true, ServiceLifetime.Transient)]
    // Scoped
    [InlineData(false, ServiceLifetime.Scoped)]
    [InlineData(true, ServiceLifetime.Scoped)]
    // Singleton
    [InlineData(false, ServiceLifetime.Singleton)]
    [InlineData(true, ServiceLifetime.Singleton)]
    public void CanAdd(bool useFactory, ServiceLifetime serviceLifetime)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (!useFactory)
            builder.IgniteSqlServerDbContext<TestDbContext>(lifetime: serviceLifetime);
        else
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(lifetime: serviceLifetime);

        var app = builder.Build();


        app.Services.GetRequiredService<TestDbContext>();
        if (useFactory) app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();


        // Test lifetime
        switch (serviceLifetime)
        {
            case ServiceLifetime.Transient:
            {
                var instance1 = app.Services.GetRequiredService<TestDbContext>();
                var instance2 = app.Services.GetRequiredService<TestDbContext>();
                Assert.NotSame(instance1, instance2);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    Assert.NotSame(factory1, factory2);
                }
            }
                break;
            case ServiceLifetime.Singleton:
            {
                var instance1 = app.Services.GetRequiredService<TestDbContext>();
                var instance2 = app.Services.GetRequiredService<TestDbContext>();
                Assert.Same(instance1, instance2);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    Assert.Same(factory1, factory2);
                }
            }
                break;
            case ServiceLifetime.Scoped:
            {
                var instance1 = app.Services.GetRequiredService<TestDbContext>();
                var instance2 = app.Services.GetRequiredService<TestDbContext>();
                Assert.Same(instance1, instance2);

                var scope = app.Services.CreateScope();
                var scopedInstance1 = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                var scopedInstance2 = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                Assert.Same(scopedInstance1, scopedInstance2);
                Assert.NotSame(scopedInstance1, instance1);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    var factory2 = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    Assert.Same(factory1, factory2);

                    var scopedFactory1 =
                        scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    var scopedFactory2 =
                        scope.ServiceProvider.GetRequiredService<IDbContextFactory<TestDbContext>>();
                    Assert.Same(scopedFactory1, scopedFactory2);
                    Assert.NotSame(scopedFactory1, factory1);
                }
            }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(serviceLifetime), serviceLifetime, null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanAdd_Guard_ReconfigurationNotSupported(bool useFactory)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (useFactory)
        {
            builder.IgniteSqlServerDbContextFactory<TestDbContext>();

            // Adding the factory again is not supported
            var notSupported = false;
            try
            {
                builder.IgniteSqlServerDbContextFactory<TestDbContext>();
            }
            catch (ReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);

            // Adding the context after the factory is not supported
            notSupported = false;
            try
            {
                builder.IgniteSqlServerDbContext<TestDbContext>();
            }
            catch (ReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);
        }
        else
        {
            builder.IgniteSqlServerDbContext<TestDbContext>();

            // Adding the context again is not supported
            var notSupported = false;
            try
            {
                builder.IgniteSqlServerDbContext<TestDbContext>();
            }
            catch (ReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);

            // Adding the factory after the context is not supported
            notSupported = false;
            try
            {
                builder.IgniteSqlServerDbContextFactory<TestDbContext>();
            }
            catch (ReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanOverride_Settings(bool useFactory)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.TracingEnabled)}",
                true.ToString()),
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{SparkConfig.Settings}:{nameof(SqlServerDbContextSparkSettings<TestDbContext>.HealthChecksEnabled)}",
                true.ToString())
        ]);

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(configureSettings: ConfigureSettings);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(configureSettings: ConfigureSettings);


        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<SqlServerDbContextSparkSettings<TestDbContext>>();
        Assert.False(resolvedSettings.TracingEnabled);
        Assert.True(resolvedSettings.HealthChecksEnabled);


        return;

        void ConfigureSettings(SqlServerDbContextSparkSettings<TestDbContext> settings)
        {
            //Settings should have correct value from configuration
            Assert.True(settings.TracingEnabled);
            Assert.True(settings.HealthChecksEnabled);

            //Change the settings
            settings.TracingEnabled = false;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanOverride_Options(bool useFactory)
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

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{nameof(SqlServerDbContextSparkOptions<TestDbContext>.ConnectionString)}",
                initialConnectionString),
            new KeyValuePair<string, string?>(
                $"{DbContextSpark.ConfigurationSectionPath}:{nameof(TestDbContext)}:{nameof(SqlServerDbContextSparkOptions<TestDbContext>.CommandTimeout)}",
                initialCommandTimeout.ToString())
        ]);

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(configureOptions: ConfigureOptions);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(configureOptions: ConfigureOptions);


        var app = builder.Build();

        var resolvedOptions =
            app.Services.GetRequiredService<IOptions<SqlServerDbContextSparkOptions<TestDbContext>>>();
        Assert.Equal(changedConnectionString, resolvedOptions.Value.ConnectionString);
        Assert.Equal(changedCommandTimeout, resolvedOptions.Value.CommandTimeout);

        var context = app.Services.GetRequiredService<TestDbContext>();
        Assert.Equal(changedCommandTimeout, context.Database.GetCommandTimeout());
        Assert.Equal(changedConnectionString, context.Database.GetConnectionString());

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
            context = factory.CreateDbContext();
            Assert.Equal(changedCommandTimeout, context.Database.GetCommandTimeout());
            Assert.Equal(changedConnectionString, context.Database.GetConnectionString());
        }


        return;

        void ConfigureOptions(SqlServerDbContextSparkOptions<TestDbContext> options)
        {
            //Options should have correct value from configuration
            Assert.Equal(initialConnectionString, options.ConnectionString);
            Assert.Equal(initialCommandTimeout, options.CommandTimeout);

            //Change the options
            options.ConnectionString = changedConnectionString;
            options.CommandTimeout = changedCommandTimeout;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanOverride_DbContextOptions(bool useFactory)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(
                configureDbContextOptionsBuilder: ConfigureDbContextOptionsBuilder);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(
                configureDbContextOptionsBuilder: ConfigureDbContextOptionsBuilder);


        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();
        Assert.True(context.Options.FindExtension<CoreOptionsExtension>()?.IsSensitiveDataLoggingEnabled);

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
            context = factory.CreateDbContext();
            Assert.True(context.Options.FindExtension<CoreOptionsExtension>()?.IsSensitiveDataLoggingEnabled);
        }

        return;

        void ConfigureDbContextOptionsBuilder(DbContextOptionsBuilder dbContextOptionsBuilder)
        {
            //Enable sensitive data logging
            dbContextOptionsBuilder.EnableSensitiveDataLogging();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanOverride_SqlServerDbContextOptions(bool useFactory)
    {
        const int commandTimeout = 12345;

        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (useFactory)
            builder.IgniteSqlServerDbContextFactory<TestDbContext>(
                configureSqlServerDbContextOptionsBuilder: ConfigureDbContextOptionsBuilder);
        else
            builder.IgniteSqlServerDbContext<TestDbContext>(
                configureSqlServerDbContextOptionsBuilder: ConfigureDbContextOptionsBuilder);


        var app = builder.Build();

        var context = app.Services.GetRequiredService<TestDbContext>();
        Assert.Equal(commandTimeout, context.Database.GetCommandTimeout());

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<TestDbContext>>();
            context = factory.CreateDbContext();
            Assert.Equal(commandTimeout, context.Database.GetCommandTimeout());
        }

        return;

        void ConfigureDbContextOptionsBuilder(SqlServerDbContextOptionsBuilder sqlServerDbContextOptionsBuilder)
        {
            sqlServerDbContextOptionsBuilder.CommandTimeout(commandTimeout);
        }
    }
}