using ES.FX.Migrations.Abstractions;

namespace ES.FX.Migrations.Tests.Support;

/// <summary>
///     An <see cref="IMigrationsTask" /> implementation that records how many times it was applied and appends its
///     name to a shared execution log. Used to assert ordering and idempotency of a DI-driven runner.
/// </summary>
internal sealed class RecordingMigrationsTask(string name, List<string> executionLog) : IMigrationsTask
{
    public string Name { get; } = name;

    public int ApplyCount { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        ApplyCount++;
        LastCancellationToken = cancellationToken;
        executionLog.Add(Name);
        return Task.CompletedTask;
    }
}
