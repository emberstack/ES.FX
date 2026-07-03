using System.Data;
using ES.FX.Additions.Microsoft.Data.SqlClient.Queries;
using ES.FX.Shared.SqlServer.Tests.Fixtures;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Tests;

/// <summary>
///     Docker-backed coverage of <see cref="SqlServerSafeQuery" /> against a live SQL Server. These tests
///     exercise the paths that cannot be observed without a real server: the success (true) result of the
///     'SELECT 1' probe, the <c>close:false</c> branch that leaves the connection open, and the
///     already-open short-circuit that skips (Open/OpenAsync). A running Docker engine is required
///     (Testcontainers spins up MsSql via the shared <see cref="SqlServerContainerFixture" />).
/// </summary>
public class SqlServerSafeQueryFunctionalTests(SqlServerContainerFixture sqlServerFixture)
    : IClassFixture<SqlServerContainerFixture>
{
    private SqlConnection CreateConnection() => new(sqlServerFixture.GetConnectionString());

    // Gap: success (true) path — sync overload. Opens the (closed) connection, runs SELECT 1,
    // ExecuteScalar returns boxed int 1, and the 'result != null && (int)result == 1' comparison yields true.
    [Fact]
    public void ExecuteSafeQuery_ValidConnection_ReturnsTrue_AndClosesByDefault()
    {
        using var connection = CreateConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);

        var result = connection.ExecuteSafeQuery();

        Assert.True(result);
        // close defaults to true, so the finally block closes the connection.
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    // Gap: success (true) path — async overload.
    [Fact]
    public async Task ExecuteSafeQueryAsync_ValidConnection_ReturnsTrue_AndClosesByDefault()
    {
        await using var connection = CreateConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);

        var result = await connection.ExecuteSafeQueryAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    // Gap: close:false branch (sync) — the 'if (close)' in finally is skipped, leaving the connection Open.
    [Fact]
    public void ExecuteSafeQuery_CloseFalse_LeavesConnectionOpen()
    {
        using var connection = CreateConnection();

        var result = connection.ExecuteSafeQuery(close: false);

        Assert.True(result);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Gap: close:false branch (async).
    [Fact]
    public async Task ExecuteSafeQueryAsync_CloseFalse_LeavesConnectionOpen()
    {
        await using var connection = CreateConnection();

        var result = await connection.ExecuteSafeQueryAsync(
            close: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Gap: already-open short-circuit (sync) — connection.State == Open, so Open() is skipped. The probe
    // still runs and returns true. With close:false the connection remains open afterwards.
    [Fact]
    public void ExecuteSafeQuery_AlreadyOpenConnection_SkipsReopen_AndProbesSuccessfully()
    {
        using var connection = CreateConnection();
        connection.Open();
        Assert.Equal(ConnectionState.Open, connection.State);

        var result = connection.ExecuteSafeQuery(close: false);

        Assert.True(result);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Gap: already-open short-circuit (async) — connection.State == Open, so OpenAsync is skipped.
    [Fact]
    public async Task ExecuteSafeQueryAsync_AlreadyOpenConnection_SkipsReopen_AndProbesSuccessfully()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.Open, connection.State);

        var result = await connection.ExecuteSafeQueryAsync(
            close: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    // Cross-check: an already-open connection with the default close:true is closed by the finally block,
    // confirming the finally acts regardless of the connection's initial state.
    [Fact]
    public void ExecuteSafeQuery_AlreadyOpenConnection_DefaultClose_ClosesConnection()
    {
        using var connection = CreateConnection();
        connection.Open();

        var result = connection.ExecuteSafeQuery();

        Assert.True(result);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}
