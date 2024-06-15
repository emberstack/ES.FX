using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

public class SqlServerDbContextConnectTests(SqlServerContainerFixture sqlServerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task AddSqlServerClient_CanConnect()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.AddSqlServerClient("database",
            configureOptions: options => { options.ConnectionString = sqlServerFixture.GetConnectionString(); }
        );

        var app = builder.Build();


        var connection = app.Services.GetRequiredService<SqlConnection>();
        await connection.OpenAsync();
    }
}