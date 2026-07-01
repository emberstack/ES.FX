namespace ES.FX.Ignite.Swashbuckle.Configuration;

/// <summary>
///     Provides the settings for using <see cref="Swashbuckle" />
/// </summary>
/// <remarks>
///     <b>Warning:</b> both the OpenAPI document and the Swagger UI are enabled by default with no
///     environment gating — they are served in every environment, including Production, unless you turn them
///     off. It is recommended to disable them in Production (for example by setting
///     <see cref="SwaggerEnabled" /> and/or <see cref="SwaggerUIEnabled" /> to <see langword="false" /> in
///     <c>appsettings.Production.json</c>) to avoid exposing your API surface.
/// </remarks>
public class SwashbuckleSparkSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether Swagger is enabled
    /// </summary>
    /// <remarks>
    ///     Enabled by default with no environment gating. Consider disabling this in Production to avoid
    ///     exposing the OpenAPI document.
    /// </remarks>
    public bool SwaggerEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether SwaggerUI is enabled
    /// </summary>
    /// <remarks>
    ///     Enabled by default with no environment gating. Consider disabling this in Production to avoid
    ///     exposing the interactive Swagger UI.
    /// </remarks>
    // ReSharper disable once InconsistentNaming
    public bool SwaggerUIEnabled { get; set; } = true;
}