using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Ignite.Migrations.Tests.Service;

/// <summary>
///     Behavioral regression coverage for the SHIPPED orchestrator <see cref="MigrationsService.StartAsync" /> using
///     real recording tasks (not just mock verification). These lock in the actual observable semantics of the
///     production for-loop: every registered task runs exactly once, in registration order, and the caller's
///     <see cref="CancellationToken" /> — even an already-cancelled one — is passed through unchanged with no
///     pre-flight short-circuit (the service deliberately performs no <c>ThrowIfCancellationRequested</c>).
/// </summary>
public class MigrationsServiceBehaviorTests
{
    private static MigrationsService CreateService(IServiceProvider provider, bool enabled = true) =>
        new(NullLogger<MigrationsService>.Instance,
            new MigrationsServiceSparkSettings { Enabled = enabled },
            provider);

    [Fact]
    public async Task StartAsync_RunsEveryTaskExactlyOnce_InRegistrationOrder_NotSorted()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        // Register deliberately out of alphabetical order so a "sort by name" mutation produces a different log.
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("charlie", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("alpha", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("bravo", log));
        await using var provider = services.BuildServiceProvider();

        await CreateService(provider).StartAsync(TestContext.Current.CancellationToken);

        // Exact sequence proves: all three ran (count), each once (no duplicates), and in registration order
        // (kills reverse-order, skip-a-task, and sort-by-name mutations of the production for-loop).
        Assert.Equal(new[] { "charlie", "alpha", "bravo" }, log);
    }

    [Fact]
    public async Task StartAsync_AlreadyCancelledToken_StillRunsAllTasks_AndPassesTheCancelledTokenThrough()
    {
        var log = new List<string>();
        var first = new RecordingMigrationsTask("first", log);
        var second = new RecordingMigrationsTask("second", log);
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(first);
        services.AddSingleton<IMigrationsTask>(second);
        await using var provider = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The shipped service performs NO ThrowIfCancellationRequested — it materializes the task list and runs
        // every task, forwarding the caller's token verbatim. This asserts that real behavior, so a mutation that
        // adds a pre-flight cancellation short-circuit, or that swaps in CancellationToken.None, would fail.
        await CreateService(provider).StartAsync(cts.Token);

        Assert.Equal(new[] { "first", "second" }, log);
        Assert.Equal(1, first.ApplyCount);
        Assert.Equal(1, second.ApplyCount);
        Assert.True(first.LastCancellationToken.IsCancellationRequested);
        Assert.True(second.LastCancellationToken.IsCancellationRequested);
        Assert.Equal(cts.Token, first.LastCancellationToken);
        Assert.Equal(cts.Token, second.LastCancellationToken);
    }

    [Fact]
    public async Task StartAsync_Disabled_RunsNoTasks_EvenWhenRegistered()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("should-not-run", log);
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(task);
        await using var provider = services.BuildServiceProvider();

        await CreateService(provider, false).StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(log);
        Assert.Equal(0, task.ApplyCount);
    }
}