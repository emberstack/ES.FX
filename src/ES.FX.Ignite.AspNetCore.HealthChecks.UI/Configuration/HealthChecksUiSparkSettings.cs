namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Configuration;

/// <summary>
///     Provides the settings for the <see cref="HealthChecksUiSpark" />
/// </summary>
public class HealthChecksUiSparkSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the health checks UI endpoint is enabled.
    /// </summary>
    public bool EndpointEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the health checks UI endpoint path.
    /// </summary>
    public string UiEndpointPath { get; set; } = "/health/ui";

    /// <summary>
    ///     Gets or sets the health checks UI API endpoint path.
    /// </summary>
    public string UiApiEndpointPath { get; set; } = "/health/ui/api";

    /// <summary>
    ///     Gets or sets the health checks UI webhook endpoint path.
    /// </summary>
    public string UiWebhookEndpointPath { get; set; } = "/health/ui/webhook";
}