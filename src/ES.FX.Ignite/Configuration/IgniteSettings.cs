using ES.FX.Ignite.Configuration.AspNetCore;
using ES.FX.Ignite.Configuration.Runtime;

namespace ES.FX.Ignite.Configuration;

/// <summary>
///     Settings used to configure the Ignite services
/// </summary>
public class IgniteSettings
{
    /// <summary>
    ///     Settings for Runtime
    /// </summary>
    public RuntimeSettings Runtime { get; } = new();

    /// <summary>
    ///     Settings for OpenTelemetry
    /// </summary>
    public IgniteOpenTelemetrySettings OpenTelemetry { get; } = new();

    /// <summary>
    ///     Settings for HttpClient
    /// </summary>
    public HttpClientSettings HttpClient { get; } = new();


    /// <summary>
    ///     Settings for AspNetCore
    /// </summary>
    public AspNetCoreSettings AspNetCore { get; } = new();
}