using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Exceptions;
using ES.FX.Microsoft.Data.SqlClient.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

public class HostingTests
{
    [Theory]
    //Defaults
    [InlineData(false, null, ServiceLifetime.Transient)]
    [InlineData(true, null, ServiceLifetime.Transient)]
    //Keyed defaults
    [InlineData(false, "keyed", ServiceLifetime.Transient)]
    [InlineData(true, "keyed", ServiceLifetime.Transient)]
    // Scoped
    [InlineData(false, null, ServiceLifetime.Scoped)]
    [InlineData(true, null, ServiceLifetime.Scoped)]
    // Singleton
    [InlineData(false, null, ServiceLifetime.Singleton)]
    [InlineData(true, null, ServiceLifetime.Singleton)]
    public void CanAdd(bool useFactory, string? serviceKey, ServiceLifetime serviceLifetime)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (!useFactory)
            builder.AddIgniteSqlServerClient("database", serviceKey, lifetime: serviceLifetime);
        else
            builder.AddIgniteSqlServerClientFactory("database", serviceKey, lifetime: serviceLifetime);

        var app = builder.Build();

        // Test resolution of default services
        if (serviceKey is null)
        {
            app.Services.GetRequiredService<SqlConnection>();
            if (useFactory) app.Services.GetRequiredService<ISqlConnectionFactory>();
        }

        // Test resolution of keyed services. If the service key is null, the default should be resolved
        app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
        if (useFactory) app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);

        // Test lifetime
        switch (serviceLifetime)
        {
            case ServiceLifetime.Transient:
            {
                var instance1 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                var instance2 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                Assert.NotSame(instance1, instance2);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    var factory2 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    Assert.NotSame(factory1, factory2);
                }
            }
                break;
            case ServiceLifetime.Singleton:
            {
                var instance1 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                var instance2 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                Assert.Same(instance1, instance2);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    var factory2 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    Assert.Same(factory1, factory2);
                }
            }
                break;
            case ServiceLifetime.Scoped:
            {
                var instance1 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                var instance2 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
                Assert.Same(instance1, instance2);

                var scope = app.Services.CreateScope();
                var scopedInstance1 = scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>(serviceKey);
                var scopedInstance2 = scope.ServiceProvider.GetRequiredKeyedService<SqlConnection>(serviceKey);
                Assert.Same(scopedInstance1, scopedInstance2);
                Assert.NotSame(scopedInstance1, instance1);

                if (useFactory)
                {
                    var factory1 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    var factory2 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    Assert.Same(factory1, factory2);

                    var scopedFactory1 =
                        scope.ServiceProvider.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
                    var scopedFactory2 =
                        scope.ServiceProvider.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
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
    //Defaults
    [InlineData(false, null)]
    [InlineData(true, null)]
    //Keyed
    [InlineData(false, "keyed")]
    [InlineData(true, "keyed")]
    public void CanAdd_Keyed(bool useFactory, string? serviceKey)
    {
        const string secondServiceKey = "client2";

        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure settings
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:database1:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                "Data Source=local;Database=database1"),
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:database2:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                "Data Source=local;Database=database2")
        ]);

        if (useFactory)
        {
            builder.AddIgniteSqlServerClientFactory("database1", serviceKey);
            builder.AddIgniteSqlServerClientFactory("database2", secondServiceKey);
        }
        else
        {
            builder.AddIgniteSqlServerClient("database1", serviceKey);
            builder.AddIgniteSqlServerClient("database2", secondServiceKey);
        }


        var app = builder.Build();

        var connection1 = app.Services.GetRequiredKeyedService<SqlConnection>(serviceKey);
        var connection2 = app.Services.GetRequiredKeyedService<SqlConnection>(secondServiceKey);

        Assert.NotSame(connection1, connection2);

        Assert.Equal("database1", connection1.Database);
        Assert.Equal("database2", connection2.Database);


        var options1 = app.Services.GetRequiredService<IOptionsMonitor<SqlServerClientSparkOptions>>()
            .Get(serviceKey ?? Options.DefaultName);
        var options2 = app.Services.GetRequiredService<IOptionsMonitor<SqlServerClientSparkOptions>>()
            .Get(secondServiceKey);
        Assert.Contains("database1", options1.ConnectionString);
        Assert.Contains("database2", options2.ConnectionString);


        var settings1 = app.Services.GetRequiredKeyedService<SqlServerClientSparkSettings>(serviceKey);
        var settings2 = app.Services.GetRequiredKeyedService<SqlServerClientSparkSettings>(secondServiceKey);

        Assert.NotSame(settings1, settings2);

        if (useFactory)
        {
            var factory1 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(serviceKey);
            var factory2 = app.Services.GetRequiredKeyedService<ISqlConnectionFactory>(secondServiceKey);
            Assert.NotSame(factory1, factory2);


            connection1 = factory1.CreateConnection();
            connection2 = factory2.CreateConnection();

            Assert.NotSame(connection1, connection2);

            Assert.Equal("database1", connection1.Database);
            Assert.Equal("database2", connection2.Database);
        }
    }

    [Theory]
    //Defaults
    [InlineData(false, null)]
    [InlineData(true, null)]
    //Keyed
    [InlineData(false, "keyed")]
    [InlineData(true, "keyed")]
    public void CanAdd_Guard_ReconfigurationNotSupported(bool useFactory, string? serviceKey)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        if (useFactory)
        {
            builder.AddIgniteSqlServerClientFactory("database", serviceKey);

            // Adding the factory again is not supported
            var notSupported = false;
            try
            {
                builder.AddIgniteSqlServerClientFactory("database", serviceKey);
            }
            catch (SparkReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);

            // Adding the client after the factory is not supported
            notSupported = false;
            try
            {
                builder.AddIgniteSqlServerClient("database", serviceKey);
            }
            catch (SparkReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);
        }
        else
        {
            builder.AddIgniteSqlServerClient("database", serviceKey);

            // Adding the client again is not supported
            var notSupported = false;
            try
            {
                builder.AddIgniteSqlServerClient("database", serviceKey);
            }
            catch (SparkReconfigurationNotSupportedException)
            {
                notSupported = true;
            }

            Assert.True(notSupported);

            // Adding the factory after the client is not supported
            notSupported = false;
            try
            {
                builder.AddIgniteSqlServerClientFactory("database", serviceKey);
            }
            catch (SparkReconfigurationNotSupportedException)
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
        const string name = "database";
        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure settings
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:{name}:{SparkConfig.Settings}:{nameof(SqlServerClientSparkSettings.TracingEnabled)}",
                true.ToString()),
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:{name}:{SparkConfig.Settings}:{nameof(SqlServerClientSparkSettings.HealthChecksEnabled)}",
                true.ToString())
        ]);

        if (useFactory)
            builder.AddIgniteSqlServerClientFactory(name, configureSettings: ConfigureSettings);
        else
            builder.AddIgniteSqlServerClient(name, configureSettings: ConfigureSettings);


        var app = builder.Build();

        var resolvedSettings = app.Services.GetRequiredService<SqlServerClientSparkSettings>();
        Assert.False(resolvedSettings.TracingEnabled);
        Assert.True(resolvedSettings.HealthChecksEnabled);


        return;

        void ConfigureSettings(SqlServerClientSparkSettings settings)
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
        const string name = "database";
        var initialConnectionString = new SqlConnectionStringBuilder
        {
            InitialCatalog = "InitialDatabase",
            DataSource = "InitialServer"
        }.ConnectionString;

        var changedConnectionString = new SqlConnectionStringBuilder(initialConnectionString)
        {
            InitialCatalog = "ChangedDatabase"
        }.ConnectionString;


        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure options
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:{name}:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                initialConnectionString)
        ]);

        if (useFactory)
            builder.AddIgniteSqlServerClientFactory(name, configureOptions: ConfigureOptions);
        else
            builder.AddIgniteSqlServerClient(name, configureOptions: ConfigureOptions);


        var app = builder.Build();

        var resolvedOptions = app.Services.GetRequiredService<IOptions<SqlServerClientSparkOptions>>();
        Assert.Equal(changedConnectionString, resolvedOptions.Value.ConnectionString);

        var connection = app.Services.GetRequiredService<SqlConnection>();
        Assert.Equal(changedConnectionString, connection.ConnectionString);

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<ISqlConnectionFactory>();
            connection = factory.CreateConnection();
            Assert.Equal(changedConnectionString, connection.ConnectionString);
        }


        return;

        void ConfigureOptions(SqlServerClientSparkOptions options)
        {
            //Options should have correct value from configuration
            Assert.Equal(initialConnectionString, options.ConnectionString);

            //Change the options
            options.ConnectionString = changedConnectionString;
        }
    }
}