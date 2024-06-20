namespace ES.FX.Ignite.FluentValidation.Configuration;

/// <summary>
///     Provides the settings for using <see cref="FluentValidation" />
/// </summary>
public class FluentValidationSparkSettings
{
    /// <summary>
    ///     Gets or sets a boolean value that indicates whether auto validation is enabled for endpoints
    /// </summary>
    public bool EndpointsAutoValidationEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a boolean value that indicates whether auto validation is enabled for MVC pipeline
    /// </summary>
    public bool MvcAutoValidationEnabled { get; set; } = true;
}