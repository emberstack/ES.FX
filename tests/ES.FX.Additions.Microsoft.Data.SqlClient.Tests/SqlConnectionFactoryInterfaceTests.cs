using ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Verifies the default implementation supplied by <see cref="ISqlConnectionFactory" />.
///     The default <c>CreateConnectionAsync</c> must delegate to the (custom) synchronous
///     <c>CreateConnection</c> and honour cancellation before doing so.
/// </summary>
public class SqlConnectionFactoryInterfaceTests
{
    [Fact]
    public async Task DefaultCreateConnectionAsync_DelegatesToCreateConnection()
    {
        var factory = new CountingFactory();

        var connection =
            await ((ISqlConnectionFactory)factory).CreateConnectionAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(connection);
        Assert.Equal(1, factory.CreateConnectionCalls);
    }

    [Fact]
    public async Task DefaultCreateConnectionAsync_HonoursCancellationBeforeCreatingConnection()
    {
        var factory = new CountingFactory();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((ISqlConnectionFactory)factory).CreateConnectionAsync(cts.Token));
        Assert.Equal(0, factory.CreateConnectionCalls);
    }

    [Fact]
    public async Task CustomImplementation_MayOverrideCreateConnectionAsync()
    {
        var expected = new SqlConnection();
        ISqlConnectionFactory factory = new OverridingFactory(expected);

        var connection = await factory.CreateConnectionAsync(TestContext.Current.CancellationToken);

        Assert.Same(expected, connection);
        expected.Dispose();
    }

    private sealed class CountingFactory : ISqlConnectionFactory
    {
        public int CreateConnectionCalls { get; private set; }

        public SqlConnection CreateConnection()
        {
            CreateConnectionCalls++;
            return new SqlConnection();
        }
    }

    private sealed class OverridingFactory(SqlConnection connection) : ISqlConnectionFactory
    {
        public SqlConnection CreateConnection() => throw new InvalidOperationException("Should not be called.");

        public Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(connection);
        }
    }
}