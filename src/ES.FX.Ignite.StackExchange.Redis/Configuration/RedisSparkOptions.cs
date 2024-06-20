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
    public string? ConnectionString { get; set; }


    /// <summary>
    ///     The configuration options for <see cref="IConnectionMultiplexer" />
    /// </summary>
    public ConfigurationOptions? ConfigurationOptions { get; set; }
}