using ES.FX.Migrations.Abstractions;
using ES.FX.Migrations.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Migrations.Tests;

/// <summary>
///     Regression coverage for the ENTIRE shipped surface of <c>ES.FX.Migrations</c>: the marker abstraction
///     <see cref="IMigrationsTask" />. The library contains no executable orchestration — the sequential runner lives in
///     <c>ES.FX.Ignite.Migrations</c> (<c>MigrationsService</c>) and is exercised, against the real type, in
///     <c>ES.FX.Ignite.Migrations.Tests</c>. These tests therefore assert only what this package actually ships: the
///     interface's method signature (defaulted <see cref="CancellationToken" />), its awaitability, and its
///     resolvability through DI in the canonical registration shapes documented for consumers. They deliberately do NOT
///     reimplement or claim to guard the orchestrator's ordering/failure/cancellation behavior.
/// </summary>
public class MigrationsTaskTests
{
    [Fact]
    public async Task ApplyMigrations_DefaultCancellationTokenParameter_IsNone()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("a", log);

        // The interface declares a defaulted CancellationToken parameter. Invoking through the interface without
        // supplying a token must yield CancellationToken.None. Passing a token here would defeat the assertion.
#pragma warning disable xUnit1051
        await ((IMigrationsTask)task).ApplyMigrations();
#pragma warning restore xUnit1051

        Assert.Equal(CancellationToken.None, task.LastCancellationToken);
        Assert.Equal(1, task.ApplyCount);
    }

    [Fact]
    public async Task ApplyMigrations_ForwardsSuppliedCancellationToken_Unchanged()
    {
        var log = new List<string>();
        var task = new RecordingMigrationsTask("a", log);

        using var cts = new CancellationTokenSource();

        await ((IMigrationsTask)task).ApplyMigrations(cts.Token);

        Assert.Equal(cts.Token, task.LastCancellationToken);
    }

    [Fact]
    public async Task ApplyMigrations_ReturnedTask_IsAwaitedToCompletion()
    {
        var completed = false;
        IMigrationsTask task = new AsyncCompletingTask(() => completed = true);

        await task.ApplyMigrations(TestContext.Current.CancellationToken);

        // Awaiting the returned Task must observe the async body's completion, not a fire-and-forget hand-off.
        Assert.True(completed);
    }

    [Fact]
    public async Task ApplyMigrations_FaultedTask_SurfacesOriginalExceptionInstance()
    {
        var boom = new InvalidOperationException("migration failed");
        IMigrationsTask task = new ThrowingMigrationsTask(boom);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.ApplyMigrations(TestContext.Current.CancellationToken));

        // The exact faulting exception must propagate through the awaited Task — no wrapping, no swallowing.
        Assert.Same(boom, thrown);
    }

    [Fact]
    public async Task ImplementationRegisteredAsInterface_ResolvesAndInvokes_ViaDependencyInjection()
    {
        // Guards the canonical registration shape from the docs: AddTransient<IMigrationsTask, TImpl>().
        var services = new ServiceCollection();
        services.AddTransient<IMigrationsTask, ResolvableTask>();
        await using var provider = services.BuildServiceProvider();

        var resolved = provider.GetService<IMigrationsTask>();

        Assert.NotNull(resolved);
        var typed = Assert.IsType<ResolvableTask>(resolved);
        await resolved.ApplyMigrations(TestContext.Current.CancellationToken);
        Assert.True(typed.Applied);
    }

    [Fact]
    public async Task MultipleImplementations_AllResolvable_ViaGetServices_InRegistrationOrder()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        // Registration order deliberately not alphabetical — GetServices must preserve registration order.
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("third", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("first", log));
        services.AddSingleton<IMigrationsTask>(new RecordingMigrationsTask("second", log));
        await using var provider = services.BuildServiceProvider();

        var resolved = provider.GetServices<IMigrationsTask>().ToList();

        // GetServices<IMigrationsTask>() is exactly how the shipped MigrationsService enumerates tasks; assert the
        // container yields all registrations in registration order (the ordering the orchestrator depends on).
        Assert.Equal(3, resolved.Count);
        foreach (var task in resolved) await task.ApplyMigrations(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "third", "first", "second" }, log);
    }

    private sealed class ResolvableTask : IMigrationsTask
    {
        public bool Applied { get; private set; }

        public Task ApplyMigrations(CancellationToken cancellationToken = default)
        {
            Applied = true;
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncCompletingTask(Action onCompleted) : IMigrationsTask
    {
        public async Task ApplyMigrations(CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            onCompleted();
        }
    }
}
