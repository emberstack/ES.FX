namespace ES.FX.Migrations.Abstractions;

/// <summary>
///     Represents a task that applies migrations
/// </summary>
public interface IMigrationsTask
{
    /// <summary>
    ///     Applies the pending migrations
    /// </summary>
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}