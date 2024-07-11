namespace ES.FX.Ignite.Hosting.Configuration;

/// <summary>
///     Settings for AspNetCore
/// </summary>
public class IgniteAspNetCoreSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the Endpoints API Explorer is enabled
    /// </summary>
    public bool EndpointsApiExplorerEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the ProblemDetails middleware is enabled
    /// </summary>
    public bool ProblemDetailsEnabled { get; set; } = true;
}