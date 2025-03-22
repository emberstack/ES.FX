namespace ES.FX.Ignite.Configuration;

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
    ///     Gets or sets a value indicating whether the JsonStringEnumConverter is enabled
    /// </summary>
    public bool JsonStringEnumConverterEnabled { get; set; } = true;


    /// <summary>
    ///     Gets or sets a value indicating whether the ForwardedHeaders middleware is enabled
    /// </summary>
    public bool ForwardedHeadersEnabled { get; set; } = true;


    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="QueryStringToHeaderMiddleware" /> is enabled
    /// </summary>
    public bool QueryStringToHeaderMiddlewareEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="ServerTimingMiddleware" /> is enabled
    /// </summary>
    public bool ServerTimingMiddlewareEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="TraceIdentifierMiddleware" /> is enabled
    /// </summary>
    public bool TraceIdentifierMiddlewareEnabled { get; set; } = true;
}