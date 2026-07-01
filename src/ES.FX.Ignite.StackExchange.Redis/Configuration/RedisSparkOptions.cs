using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

/// <summary>
///     Provides the options for connecting to a Redis server using a <see cref="IConnectionMultiplexer" />
/// </summary>
public class RedisSparkOptions
{
    /// <summary>
    ///     The connection string for <see cref="IConnectionMultiplexer" />
    /// </summary>
    /// <remarks>
    ///     Takes full precedence over <see cref="ConfigurationOptions" />. When set to a value that is not
    ///     null or whitespace, <see cref="ConfigurationOptions" /> is ignored entirely.
    /// </remarks>
    public string? ConnectionString { get; set; }


    /// <summary>
    ///     The configuration options for <see cref="IConnectionMultiplexer" />
    /// </summary>
    /// <remarks>
    ///     Only used when <see cref="ConnectionString" /> is null or whitespace. When
    ///     <see cref="ConnectionString" /> is set, this property is ignored entirely, including members
    ///     that cannot be expressed in a connection string (such as
    ///     <see cref="global::StackExchange.Redis.ConfigurationOptions.CertificateValidation" /> or
    ///     <see cref="global::StackExchange.Redis.ConfigurationOptions.ReconnectRetryPolicy" />).
    /// </remarks>
    public ConfigurationOptions? ConfigurationOptions { get; set; }
}