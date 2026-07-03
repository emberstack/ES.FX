using ES.FX.Migrations.Abstractions;

namespace ES.FX.Migrations.Tests.Support;

/// <summary>
///     An <see cref="IMigrationsTask" /> that always fails when applied. Used to assert a sequential runner
///     surfaces task failures and stops running later tasks.
/// </summary>
internal sealed class ThrowingMigrationsTask(Exception exception) : IMigrationsTask
{
    public Task ApplyMigrations(CancellationToken cancellationToken = default) => Task.FromException(exception);
}
