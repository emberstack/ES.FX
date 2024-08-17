using ES.FX.Ignite.Spark.Configuration.Abstractions;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

/// <summary>
///     Provides the settings for connecting to a Redis server using a <see cref="IConnectionMultiplexer" />
/// </summary>
public class RedisSparkSettings
{
    /// <summary>
    ///     <inheritdoc cref="SparkHealthCheckSettings" />
    /// </summary>
    public SparkHealthCheckSettings HealthChecks { get; set; } = new();


    /// <summary>
    ///     <inheritdoc cref="SparkTracingSettings" />
    /// </summary>
    public SparkTracingSettings Tracing { get; set; } = new();
}