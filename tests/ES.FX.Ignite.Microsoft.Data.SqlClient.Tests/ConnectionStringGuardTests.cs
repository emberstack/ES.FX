using ES.FX.Ignite.Microsoft.Data.SqlClient.Configuration;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Verifies the fail-fast guard in <c>GetRequiredConnectionString</c>: resolving a
///     <see cref="SqlConnection" /> with a missing/blank connection string must throw a descriptive
///     <see cref="InvalidOperationException" /> rather than silently constructing a broken connection.
/// </summary>
public class ConnectionStringGuardTests
{
    [Theory]
    [InlineData(null)] // ConnectionString never configured
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace only
    public void ResolveSqlConnection_Throws_When_ConnectionString_Missing(string? connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database", configureOptions: options =>
        {
            if (connectionString is not null) options.ConnectionString = connectionString;
        });

        var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            app.Services.GetRequiredService<SqlConnection>());

        // The message must be the descriptive fail-fast message (mentions the name and the config path),
        // proving the guard ran rather than new SqlConnection(null/"") being reached.
        Assert.Contains("ConnectionString is missing", ex.Message);
        Assert.Contains("database", ex.Message);
        Assert.Contains(nameof(SqlServerClientSparkOptions.ConnectionString), ex.Message);
    }

    [Fact]
    public void ResolveSqlConnection_Throws_For_Keyed_When_ConnectionString_Missing()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database", serviceKey: "primary");

        var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            app.Services.GetRequiredKeyedService<SqlConnection>("primary"));

        Assert.Contains("ConnectionString is missing", ex.Message);
    }

    [Fact]
    public void ResolveSqlConnection_Succeeds_When_ConnectionString_Present()
    {
        const string connectionString = "Server=(local);Database=x;";
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.IgniteSqlServerClient("database",
            configureOptions: options => options.ConnectionString = connectionString);

        var app = builder.Build();

        // A present connection string must flow through to the SqlConnection untouched.
        var connection = app.Services.GetRequiredService<SqlConnection>();
        Assert.Equal(connectionString, connection.ConnectionString);
    }
}
