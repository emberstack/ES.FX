namespace ES.FX.Ignite.Configuration;

/// <summary>
///     Settings used to configure the Ignite services
/// </summary>
public class IgniteSettings
{
    /// <summary>
    ///     Settings for Configuration
    /// </summary>
    public IgniteConfigurationSettings Configuration { get; } = new();

    /// <summary>
    ///     Settings for HealthChecks
    /// </summary>
    public IgniteHealthChecksSettings HealthChecks { get; } = new();

    /// <summary>
    ///     Settings for OpenTelemetry
    /// </summary>
    public IgniteOpenTelemetrySettings OpenTelemetry { get; } = new();

    /// <summary>
    ///     Settings for HttpClient
    /// </summary>
    public IgniteHttpClientSettings HttpClient { get; } = new();


    /// <summary>
    ///     Settings for AspNetCore
    /// </summary>
    public IgniteAspNetCoreSettings AspNetCore { get; } = new();
}