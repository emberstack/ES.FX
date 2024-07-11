namespace ES.FX.Ignite.Swashbuckle.Configuration;

/// <summary>
///     Provides the settings for using <see cref="Swashbuckle" />
/// </summary>
public class SwashbuckleSparkSettings
{
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