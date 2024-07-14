namespace ES.FX.Ignite.Configuration;

/// <summary>
///     Settings for HTTP client
/// </summary>
public class IgniteHttpClientSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the standard resilience handler is enabled
    /// </summary>
    public bool StandardResilienceHandlerEnabled { get; set; } = true;
}