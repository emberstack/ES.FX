using ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Factories;

/// <summary>
///     Defines a factory for creating instances of <see cref="SqlConnection" /> using a delegate.
/// </summary>
/// <param name="serviceProvider">Service provider used by the factory</param>
/// <param name="factory">Factory function used to create the <see cref="SqlConnection" /></param>
[PublicAPI]
public class DelegateSqlConnectionFactory(
    IServiceProvider serviceProvider,
    Func<IServiceProvider, SqlConnection> factory)
    : ISqlConnectionFactory
{
    public SqlConnection CreateConnection() => factory(serviceProvider);
}