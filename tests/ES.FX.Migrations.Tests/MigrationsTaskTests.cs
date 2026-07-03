using ES.FX.Migrations.Abstractions;
using ES.FX.Migrations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.Migrations.Tests;

/// <summary>
///     Functional regression coverage for the <see cref="ES.FX.Migrations" /> public surface: the
///     <see cref="IMigrationsTask" /> abstraction and its DI-driven consumption. The runner under exercise
///     (<see cref="MigrationsTaskRunner" />) is a faithful reimplementation of the sequential, registration-order
///     behavior the Ignite migrations service relies on, so these tests guard the abstraction against future
///     library and package changes.
/// </summary>
public class MigrationsTaskTests
{
    [Fact]
    public async Task Runner_Applies_All_Registered_Tasks()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("a", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("b", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("c", log));
        await using var provider = services.BuildServiceProvider();

        var applied = await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);

        Assert.Equal(3, applied);
        Assert.Equal(new[] { "a", "b", "c" }, log);
    }

    [Fact]
    public async Task Runner_Applies_Tasks_In_Registration_Order()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        // Register out of alphabetical order to prove the runner honors registration order, not naming.
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("third", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("first", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("second", log));
        await using var provider = services.BuildServiceProvider();

        await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "third", "first", "second" }, log);
    }

    [Fact]
    public async Task Runner_Is_Idempotent_Second_Run_Reapplies_Same_Tasks_Same_Order()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        var taskA = new RecordingMigrationsTask("a", log);
        var taskB = new RecordingMigrationsTask("b", log);
        services.AddSingleton<IMigrationsTask>(taskA);
        services.AddSingleton<IMigrationsTask>(taskB);
        await using var provider = services.BuildServiceProvider();

        var firstApplied = await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);
        var secondApplied = await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);

        // Idempotent from the runner's perspective: same set of tasks, same order, no drift or duplication.
        Assert.Equal(2, firstApplied);
        Assert.Equal(2, secondApplied);
        Assert.Equal(new[] { "a", "b", "a", "b" }, log);
        Assert.Equal(2, taskA.ApplyCount);
        Assert.Equal(2, taskB.ApplyCount);
    }

    [Fact]
    public async Task Runner_With_No_Registered_Tasks_Is_A_Noop()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();

        var applied = await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);

        Assert.Equal(0, applied);
    }

    [Fact]
    public async Task ApplyMigrations_Default_CancellationToken_Is_None()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("a", log);

        // The interface declares a defaulted CancellationToken parameter; calling without one must yield None.
        // Deliberately omit the token here — passing one would defeat the purpose of this assertion.
#pragma warning disable xUnit1051
        await ((IMigrationsTask)task).ApplyMigrations();
#pragma warning restore xUnit1051

        Assert.Equal(CancellationToken.None, task.LastCancellationToken);
        Assert.Equal(1, task.ApplyCount);
    }

    [Fact]
    public async Task Runner_Propagates_CancellationToken_To_Tasks()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("a", log);
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(task);
        await using var provider = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        await MigrationsTaskRunner.RunAsync(provider, cts.Token);

        Assert.Equal(cts.Token, task.LastCancellationToken);
    }

    [Fact]
    public async Task Runner_Does_Not_Run_Tasks_When_Cancelled_Before_Start()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("a", log);
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(task);
        await using var provider = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Deliberately drive the runner with an already-cancelled token to assert it short-circuits.
#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => MigrationsTaskRunner.RunAsync(provider, cts.Token));
#pragma warning restore xUnit1051

        Assert.Equal(0, task.ApplyCount);
        Assert.Empty(log);
    }

    [Fact]
    public async Task Runner_Surfaces_Task_Failure_And_Stops_Sequential_Execution()
    {
        var log = new List<string>();
        var boom = new InvalidOperationException("migration failed");
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("before", log));
        services.AddSingleton<IMigrationsTask>(new ThrowingMigrationsTask(boom));
        var after = new RecordingMigrationsTask("after", log);
        services.AddSingleton<IMigrationsTask>(after);
        await using var provider = services.BuildServiceProvider();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken));

        Assert.Same(boom, thrown);
        // The failing task aborts the run; the task registered after it must not have executed.
        Assert.Equal(new[] { "before" }, log);
        Assert.Equal(0, after.ApplyCount);
    }

    [Fact]
    public async Task Task_Registered_Via_Interface_Is_Resolvable_As_IMigrationsTask()
    {
        // Guards the canonical registration shape from the docs: AddTransient<IMigrationsTask, TImpl>().
        var services = new ServiceCollection();
        services.AddTransient<IMigrationsTask, ResolvableTask>();
        await using var provider = services.BuildServiceProvider();

        var resolved = provider.GetService<IMigrationsTask>();

        Assert.NotNull(resolved);
        Assert.IsType<ResolvableTask>(resolved);
        await resolved.ApplyMigrations(TestContext.Current.CancellationToken);
        Assert.True(ResolvableTask.WasApplied);
    }

    [Fact]
    public async Task Runner_Awaits_Async_Task_To_Completion()
    {
        var completed = false;
        var mock = new Mock<IMigrationsTask>();
        mock.Setup(m => m.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                await Task.Yield();
                completed = true;
            });

        var services = new ServiceCollection();
        services.AddSingleton(mock.Object);
        await using var provider = services.BuildServiceProvider();

        await MigrationsTaskRunner.RunAsync(provider, TestContext.Current.CancellationToken);

        Assert.True(completed);
        mock.Verify(m => m.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class ResolvableTask : IMigrationsTask
    {
        public static bool WasApplied { get; private set; }

        public Task ApplyMigrations(CancellationToken cancellationToken = default)
        {
            WasApplied = true;
            return Task.CompletedTask;
        }
    }
}
