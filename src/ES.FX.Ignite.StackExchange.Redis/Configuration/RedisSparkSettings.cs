﻿using ES.FX.Ignite.Spark.Configuration.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

/// <summary>
///     Provides the settings for connecting to a Redis server using a <see cref="IConnectionMultiplexer" />
/// </summary>
public class RedisSparkSettings : ISparkHealthCheckSettings, ISparkTracingSettings
{
    /// <summary>
    ///     <inheritdoc cref="ISparkHealthCheckSettings.HealthChecksEnabled" />
    /// </summary>
    public bool HealthChecksEnabled { get; set; } = true;


    /// <summary>
    ///     <inheritdoc cref="ISparkHealthCheckSettings.HealthChecksFailureStatus" />
    /// </summary>
    public HealthStatus? HealthChecksFailureStatus { get; set; }


    /// <summary>
    ///     <inheritdoc cref="ISparkTracingSettings.TracingEnabled" />
    /// </summary>
    public bool TracingEnabled { get; set; } = true;
}