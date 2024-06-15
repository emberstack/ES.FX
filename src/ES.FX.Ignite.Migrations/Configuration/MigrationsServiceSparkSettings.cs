using ES.FX.Ignite.Migrations.Service;

namespace ES.FX.Ignite.Migrations.Configuration;

/// <summary>
///     Provides the settings for the <see cref="MigrationsService" />
/// </summary>
public class MigrationsServiceSparkSettings
{
    public bool Enabled { get; set; }
    public bool ExitOnComplete { get; set; }
}