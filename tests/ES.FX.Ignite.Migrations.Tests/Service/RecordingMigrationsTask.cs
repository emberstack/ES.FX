using ES.FX.Migrations.Abstractions;

namespace ES.FX.Ignite.Migrations.Tests.Service;

/// <summary>
///     An <see cref="IMigrationsTask" /> that records, into a shared log, the order in which it ran and the exact
///     <see cref="CancellationToken" /> it received. Unlike a bare Moq stub, it captures observable state that lets a
///     test assert real ordering, task count, and token propagation of the shipped <see cref="MigrationsService" />.
/// </summary>
internal sealed class RecordingMigrationsTask(string name, List<string> executionLog) : IMigrationsTask
{
    public int ApplyCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        ApplyCount++;
        LastCancellationToken = cancellationToken;
        executionLog.Add(name);
        return Task.CompletedTask;
    }
}
