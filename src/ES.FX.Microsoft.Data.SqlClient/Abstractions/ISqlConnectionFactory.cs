using JetBrains.Annotations;
using Microsoft.Data.SqlClient;

namespace ES.FX.Microsoft.Data.SqlClient.Abstractions;

/// <summary>
///     Defines a factory for creating <see cref="SqlConnection" /> instances.
/// </summary>
[PublicAPI]
public interface ISqlConnectionFactory
{
    /// <summary>
    ///     Creates a new <see cref="SqlConnection" /> instance.
    /// </summary>
    SqlConnection CreateConnection();


    /// <summary>
    ///     Creates a new <see cref="SqlConnection" /> instance in an async context.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
    /// <returns>A task containing the created <see cref="SqlConnection" /> that represents the asynchronous operation.</returns>
    /// <exception cref="OperationCanceledException">If the <see cref="CancellationToken" /> is canceled.</exception>
    Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateConnection());
}