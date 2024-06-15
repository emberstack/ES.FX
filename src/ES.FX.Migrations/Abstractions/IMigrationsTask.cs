namespace ES.FX.Migrations.Abstractions;

public interface IMigrationsTask
{
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}