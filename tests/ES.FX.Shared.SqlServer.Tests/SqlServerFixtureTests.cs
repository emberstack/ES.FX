using ES.FX.Extensions.Microsoft.Data.SqlClient.Queries;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace ES.FX.Shared.SqlServer.Tests;

public class SqlServerFixtureTests(SqlServerContainerFixture sqlServerContainerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public void SqlServerContainer_CanConnect()
    {
        var connectionString = sqlServerContainerFixture.GetConnectionString();
        var connection = new SqlConnection(connectionString);
        var result = connection.ExecuteSafeQuery();
        Assert.True(result);
    }
}