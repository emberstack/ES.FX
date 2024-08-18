using ES.FX.Migrations.Abstractions;

namespace ES.FX.Ignite.Migrations.Tests.Service;

internal class TestMigrationTask : IMigrationsTask
{
    public bool ApplyMigrationsCalled { get; private set; }

    Task IMigrationsTask.ApplyMigrations(CancellationToken cancellationToken)
    {
        ApplyMigrationsCalled = true;
        return Task.CompletedTask;
    }
}