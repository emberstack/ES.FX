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

    /// <summary>
    ///     Gets or sets a value indicating whether SwaggerGen is enabled
    /// </summary>
    public bool SwaggerGenEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether Swagger is enabled
    /// </summary>
    public bool SwaggerEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether SwaggerUI is enabled
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public bool SwaggerUIEnabled { get; set; } = true;
}