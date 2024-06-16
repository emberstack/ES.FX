using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Spark;
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Microsoft.Data.SqlClient.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

public class SqlServerClientHostingExtensionsTests
{
    [Fact]
    public void AddSqlServerClient_CanResolveConnection()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClient("database");

        var app = builder.Build();

        app.Services.GetRequiredService<SqlConnection>();
    }

    [Fact]
    public void AddSqlServerClientFactory_CanResolveFactoryAndConnection()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClientFactory("database");

        var app = builder.Build();

        app.Services.GetRequiredService<SqlConnection>();
        app.Services.GetRequiredService<ISqlConnectionFactory>();
    }

    [Fact]
    public void AddSqlServerClientFactory_Lifetime_Transient_FactoryHasCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClientFactory("database", lifetime: ServiceLifetime.Transient);

        var app = builder.Build();

        var factory1 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        var factory2 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        Assert.NotSame(factory1, factory2);
    }

    [Fact]
    public void AddSqlServerClientFactory_Lifetime_Singleton_FactoryHasCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClientFactory("database", lifetime: ServiceLifetime.Singleton);

        var app = builder.Build();

        var factory1 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        var factory2 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void AddSqlServerClientFactory_Lifetime_Scoped_FactoryAndConnectionHaveCorrectLifetime()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClientFactory("database", lifetime: ServiceLifetime.Scoped);

        var app = builder.Build();

        // Scoped factories should be the same within the same scope
        var factory1 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        var factory2 = app.Services.GetRequiredService<ISqlConnectionFactory>();
        Assert.Same(factory1, factory2);

        // Factory created SqlConnections should not be scoped if the factory is scoped
        var createdConnection1 = factory1.CreateConnection();
        var createdConnection2 = factory1.CreateConnection();
        Assert.NotSame(createdConnection1, createdConnection2);

        // Resolved SqlConnections should be the same within the same scope
        var resolvedConnection1 = app.Services.GetRequiredService<SqlConnection>();
        var resolvedConnection2 = app.Services.GetRequiredService<SqlConnection>();
        Assert.Same(resolvedConnection1, resolvedConnection2);


        // Factories should be different in different scopes
        var scope = app.Services.CreateScope();
        var scopedFactory1 = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        var scopedFactory2 = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        Assert.Same(scopedFactory1, scopedFactory2);
        Assert.NotSame(scopedFactory1, factory1);

        // Scope resolved SqlConnections should be the same within the same scope
        var scopedConnection1 = scope.ServiceProvider.GetRequiredService<SqlConnection>();
        var scopedConnection2 = scope.ServiceProvider.GetRequiredService<SqlConnection>();
        Assert.Same(scopedConnection1, scopedConnection2);
        Assert.NotSame(scopedConnection1, resolvedConnection1);
    }


    [Fact]
    public void AddSqlServerClient_CanChangeSettingsInCode()
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
        builder.AddSqlServerClient(name,
            configureSettings: settings =>
            {
                //Settings should have correct value from configuration
                Assert.True(settings.TracingEnabled);
                Assert.True(settings.HealthChecksEnabled);

                //Change the settings
                settings.TracingEnabled = false;
            });

        var app = builder.Build();

        var settings = app.Services.GetRequiredService<SqlServerClientSparkSettings>();
        Assert.False(settings.TracingEnabled);
        Assert.True(settings.HealthChecksEnabled);
    }

    [Fact]
    public void AddSqlServerClient_CanChangeOptionsInCode()
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
        builder.AddSqlServerClient(name,
            configureOptions: options =>
            {
                //Options should have correct value from configuration
                Assert.Equal(initialConnectionString, options.ConnectionString);

                //Change the options
                options.ConnectionString = changedConnectionString;
            });

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<SqlServerClientSparkOptions>>();
        Assert.Equal(changedConnectionString, options.Value.ConnectionString);


        var connection = app.Services.GetRequiredService<SqlConnection>();

        Assert.Equal(changedConnectionString, connection.ConnectionString);
    }


    [Fact]
    public void AddSqlServerClient_CanHaveMultipleKeyedServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        //Configure settings
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:database1:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                "Data Source=local;Database=database1"),
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:database2:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                "Data Source=local;Database=database2"),
            new KeyValuePair<string, string?>(
                $"{SqlServerClientSpark.ConfigurationSectionPath}:database3:{nameof(SqlServerClientSparkOptions.ConnectionString)}",
                "Data Source=local;Database=database3")
        ]);


        builder.AddSqlServerClient("database1");
        builder.AddSqlServerClient("database2", "database2");
        builder.AddSqlServerClient("database3", "database3");

        var app = builder.Build();

        var connection1 = app.Services.GetRequiredService<SqlConnection>();
        var connection2 = app.Services.GetRequiredKeyedService<SqlConnection>("database2");
        var connection3 = app.Services.GetRequiredKeyedService<SqlConnection>("database3");

        Assert.NotSame(connection1, connection2);
        Assert.NotSame(connection1, connection3);
        Assert.NotSame(connection2, connection3);

        Assert.Equal("database1", connection1.Database);
        Assert.Equal("database2", connection2.Database);
        Assert.Equal("database3", connection3.Database);
    }
}