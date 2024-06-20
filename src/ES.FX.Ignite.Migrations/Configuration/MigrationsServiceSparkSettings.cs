using ES.FX.Ignite.Migrations.Service;

namespace ES.FX.Ignite.Migrations.Configuration;

/// <summary>
///     Provides the settings for the <see cref="MigrationsService" />
/// </summary>
public class MigrationsServiceSparkSettings
{
    /// <summary>
    ///     Gets or sets a value indicating whether the migrations service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the migrations service should exit on complete.
    /// </summary>
    /// <remarks> Note that the service will call <see cref="Environment.Exit" /> to exit</remarks>
    public bool ExitOnComplete { get; set; } = false;
}