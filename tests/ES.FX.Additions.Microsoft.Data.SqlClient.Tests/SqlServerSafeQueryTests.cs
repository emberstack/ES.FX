using System.Data;
using ES.FX.Additions.Microsoft.Data.SqlClient.Queries;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Dockerless coverage of <see cref="SqlServerSafeQuery" />. A live SQL Server is required to observe
///     the true / connection-open paths, so here we lock down the parts that are observable without a
///     server: the command-text contract, the argument-null guards, and the cancellation contract of the
///     async overload.
/// </summary>
public class SqlServerSafeQueryTests
{
    // Short connect timeout so the "unreachable" tests fail fast rather than hanging.
    private const string UnreachableConnectionString =
        "Server=tcp:localhost,1;Database=Test;User ID=sa;Password=Passw0rd!;" +
        "TrustServerCertificate=True;Connect Timeout=1";

    [Fact]
    public void CommandText_IsTheCanonicalConnectivityProbe()
    {
        // This constant is the public contract used by health checks; guard it against accidental edits.
        Assert.Equal("SELECT 1", SqlServerSafeQuery.CommandText);
    }

    [Fact]
    public void ExecuteSafeQuery_NullConnection_Throws()
    {
        SqlConnection connection = null!;
        Assert.Throws<ArgumentNullException>(() => connection.ExecuteSafeQuery());
    }

    [Fact]
    public async Task ExecuteSafeQueryAsync_NullConnection_Throws()
    {
        SqlConnection connection = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            connection.ExecuteSafeQueryAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExecuteSafeQuery_UnreachableServer_ReturnsFalseInsteadOfThrowing()
    {
        // Points at a closed port; the method swallows the failure and reports the connection invalid.
        using var connection = new SqlConnection(UnreachableConnectionString);

        var result = connection.ExecuteSafeQuery();

        Assert.False(result);
        // The finally block closes the connection regardless of outcome.
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task ExecuteSafeQueryAsync_UnreachableServer_ReturnsFalseInsteadOfThrowing()
    {
        await using var connection = new SqlConnection(UnreachableConnectionString);

        var result = await connection.ExecuteSafeQueryAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task ExecuteSafeQueryAsync_CancelledToken_Throws()
    {
        await using var connection = new SqlConnection(UnreachableConnectionString);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The async overload rethrows OperationCanceledException when the token is cancelled,
        // rather than masking it as a "false" result like other failures.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            connection.ExecuteSafeQueryAsync(cancellationToken: cts.Token));
    }
}