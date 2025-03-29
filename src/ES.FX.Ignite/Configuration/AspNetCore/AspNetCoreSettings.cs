namespace ES.FX.Ignite.Configuration.AspNetCore;

/// <summary>
///     Settings for AspNetCore
/// </summary>
public class AspNetCoreSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the Endpoints API Explorer is enabled
    /// </summary>
    public bool AddEndpointsApiExplorer { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the Exception Handling Middleware is enabled
    /// </summary>
    public bool UseExceptionHandler { get; set; } = true;


    /// <summary>
    ///     Gets or sets a value indicating the StatusCodePages middleware is enabled
    /// </summary>
    public bool UseStatusCodePages { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating the DeveloperExceptionPage middleware is enabled on the Development environment
    /// </summary>
    public bool UseDeveloperExceptionPage { get; set; } = true;


    /// <summary>
    ///     Gets or sets a value indicating whether the ProblemDetails middleware is enabled
    /// </summary>
    public bool AddProblemDetails { get; set; } = true;

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
    public bool UseQueryStringToHeaderMiddleware { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="ServerTimingMiddleware" /> is enabled
    /// </summary>
    public bool UseServerTimingMiddleware { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the <see cref="TraceIdResponseHeaderMiddleware" /> is enabled
    /// </summary>
    public bool UseTraceIdResponseHeader { get; set; } = true;

    /// <summary>
    ///     Settings for HealthChecks
    /// </summary>
    public AspNetCoreHealthChecksSettings HealthChecks { get; } = new();


    /// <summary>
    ///     <inheritdoc cref="AspNetCoreTracingSettings" />
    /// </summary>
    public AspNetCoreTracingSettings Tracing { get; set; } = new();

    /// <summary>
    ///     <inheritdoc cref="AspNetCoreMetricsSettings" />
    /// </summary>
    public AspNetCoreMetricsSettings Metrics { get; set; } = new();
}