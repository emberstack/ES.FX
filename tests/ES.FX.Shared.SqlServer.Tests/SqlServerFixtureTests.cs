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
        var client = new SqlConnection(connectionString);
        client.Open();
        var command = client.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = command.ExecuteScalar();
        Assert.Equal(1, result);
        client.Close();
    }
}