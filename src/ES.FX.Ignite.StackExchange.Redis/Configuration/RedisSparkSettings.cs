using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

/// <summary>
///     Provides the settings for connecting to a Redis server using a <see cref="IConnectionMultiplexer" />
/// </summary>
public class RedisSparkSettings
{
    /// <summary>
    ///     <inheritdoc cref="HealthCheckSettings" />
    /// </summary>
    public HealthCheckSettings HealthChecks { get; set; } = new();


    /// <summary>
    ///     <inheritdoc cref="TracingSettings" />
    /// </summary>
    public TracingSettings Tracing { get; set; } = new();
}