using System.Data;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Queries;

/// <summary>
///     Provides a safe query to execute on a SQL Server database to check if the connection is valid
/// </summary>
public static class SqlServerSafeQuery
{
    /// <summary>
    ///     The command text executed to check if the connection is valid
    /// </summary>
    public const string CommandText = "SELECT 1";

    /// <summary>
    ///     Executes a safe query to check if the connection is valid
    /// </summary>
    /// <param name="connection"> The <see cref="SqlConnection" /> to execute the query on</param>
    /// <param name="close"> Indicates whether to close the connection after executing the query</param>
    /// <returns> A boolean value indicating whether the connection is valid. Returns false on any failure</returns>
    public static bool ExecuteSafeQuery(this SqlConnection connection, bool close = true)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            if (connection.State != ConnectionState.Open) connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            var result = command.ExecuteScalar();
            return result != null && (int)result == 1;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (close) connection.Close();
        }
    }

    /// <summary>
    ///     Executes a safe query to check if the connection is valid
    /// </summary>
    /// <param name="connection"> The <see cref="SqlConnection" /> to execute the query on</param>
    /// <param name="close"> Indicates whether to close the connection after executing the query</param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken" /> to cancel the operation</param>
    /// <returns> A boolean value indicating whether the connection is valid. Returns false on any failure</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    public static async Task<bool> ExecuteSafeQueryAsync(this SqlConnection connection, bool close = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        try
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = CommandText;
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result != null && (int)result == 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (close) await connection.CloseAsync().ConfigureAwait(false);
        }
    }
}