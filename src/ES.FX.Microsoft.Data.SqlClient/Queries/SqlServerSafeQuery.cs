using Microsoft.Data.SqlClient;

namespace ES.FX.Microsoft.Data.SqlClient.Queries;

/// <summary>
///     Provides a safe query to execute on a SQL Server database to check if the connection is valid
/// </summary>
public static class SqlServerSafeQuery
{
    public const string CommandText = "SELECT 1";

    /// <summary>
    ///     Executes a safe query to check if the connection is valid
    /// </summary>
    /// <param name="connection"> The <see cref="SqlConnection" /> to execute the query on</param>
    /// <param name="close"> Indicates whether to close the connection after executing the query</param>
    /// <returns> A boolean value indicating whether the connection is valid</returns>
    public static bool ExecuteSafeQuery(this SqlConnection connection, bool close = true)
    {
        try
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = CommandText;
            var result = command.ExecuteScalar();
            if (close) connection.Close();
            return result != null && (int)result == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Executes a safe query to check if the connection is valid
    /// </summary>
    /// <param name="connection"> The <see cref="SqlConnection" /> to execute the query on</param>
    /// <param name="close"> Indicates whether to close the connection after executing the query</param>
    /// <param name="cancellationToken"> The <see cref="CancellationToken" /> to cancel the operation</param>
    /// <returns> A boolean value indicating whether the connection is valid</returns>
    public static async Task<bool> ExecuteSafeQueryAsync(this SqlConnection connection, bool close = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = CommandText;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (close) connection.Close();
            return result != null && (int)result == 1;
        }
        catch
        {
            return false;
        }
    }
}