using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Microsoft.Data.SqlClient.Abstractions;
using ES.FX.Microsoft.Data.SqlClient.Queries;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

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
            builder.IgniteSqlServerClientFactory("database", configureOptions: ConfigureOptions);
        else
            builder.IgniteSqlServerClient("database", configureOptions: ConfigureOptions);

        var app = builder.Build();


        var connection = app.Services.GetRequiredService<SqlConnection>();
        var result = await connection.ExecuteSafeQueryAsync();
        Assert.True(result);

        if (useFactory)
        {
            var factory = app.Services.GetRequiredService<ISqlConnectionFactory>();
            connection = await factory.CreateConnectionAsync();
            result = await connection.ExecuteSafeQueryAsync();
            Assert.True(result);
        }

        return;

        void ConfigureOptions(SqlServerClientSparkOptions options)
        {
            options.ConnectionString = sqlServerFixture.GetConnectionString();
        }
    }
}