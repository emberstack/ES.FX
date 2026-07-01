using ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions;
using JetBrains.Annotations;
using Microsoft.Data.SqlClient;

namespace ES.FX.Additions.Microsoft.Data.SqlClient.Factories;

/// <summary>
///     Defines a factory for creating instances of <see cref="SqlConnection" /> using a delegate.
/// </summary>
[PublicAPI]
public class DelegateSqlConnectionFactory : ISqlConnectionFactory
{
    private readonly Func<IServiceProvider, SqlConnection> _factory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegateSqlConnectionFactory" /> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider used by the factory</param>
    /// <param name="factory">Factory function used to create the <see cref="SqlConnection" /></param>
    public DelegateSqlConnectionFactory(IServiceProvider serviceProvider,
        Func<IServiceProvider, SqlConnection> factory)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(factory);
        _serviceProvider = serviceProvider;
        _factory = factory;
    }

    /// <inheritdoc />
    public SqlConnection CreateConnection() => _factory(_serviceProvider);
}