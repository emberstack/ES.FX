using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ES.FX.Ignite.Migrations.Tests;

public class MigrationsServiceTests
{
    private static MigrationsService CreateService(
        MigrationsServiceSparkSettings settings,
        IServiceProvider serviceProvider) =>
        new(NullLogger<MigrationsService>.Instance, settings, serviceProvider);

    [Fact]
    public async Task StartAsync_Disabled_DoesNotResolveOrRunAnyTasks()
    {
        var task = new Mock<IMigrationsTask>(MockBehavior.Strict);

        // Track whether the container was ever asked for the tasks.
        var resolved = false;
        var services = new ServiceCollection();
        services.AddSingleton<IMigrationsTask>(_ =>
        {
            resolved = true;
            return task.Object;
        });
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = false }, provider);

        await service.StartAsync(CancellationToken.None);

        Assert.False(resolved);
        task.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_Enabled_NoTasksRegistered_CompletesWithoutError()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        // Empty task set: the loop simply does not execute and StartAsync completes.
        await service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_Enabled_SingleTask_IsInvokedOnce()
    {
        var task = new Mock<IMigrationsTask>();
        task.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(task.Object);
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        await service.StartAsync(CancellationToken.None);

        task.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_Enabled_MultipleTasks_RunSequentiallyInRegistrationOrder()
    {
        var executionOrder = new List<int>();

        var task1 = new Mock<IMigrationsTask>();
        task1.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                executionOrder.Add(1);
                return Task.CompletedTask;
            });

        var task2 = new Mock<IMigrationsTask>();
        task2.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                executionOrder.Add(2);
                return Task.CompletedTask;
            });

        var task3 = new Mock<IMigrationsTask>();
        task3.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                executionOrder.Add(3);
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(task1.Object);
        services.AddSingleton(task2.Object);
        services.AddSingleton(task3.Object);
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        await service.StartAsync(CancellationToken.None);

        // All three ran, exactly once, and in the order they were registered.
        Assert.Equal([1, 2, 3], executionOrder);
        task1.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Once);
        task2.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Once);
        task3.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_TaskThrows_PropagatesAndStopsSubsequentTasks()
    {
        var firstRan = false;
        var thirdRan = false;

        var task1 = new Mock<IMigrationsTask>();
        task1.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                firstRan = true;
                return Task.CompletedTask;
            });

        var expected = new InvalidOperationException("boom");
        var task2 = new Mock<IMigrationsTask>();
        task2.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var task3 = new Mock<IMigrationsTask>();
        task3.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                thirdRan = true;
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(task1.Object);
        services.AddSingleton(task2.Object);
        services.AddSingleton(task3.Object);
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.StartAsync(CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.True(firstRan, "The first task should have run before the failure.");
        Assert.False(thirdRan, "The task after the failing one must not run.");
        task3.Verify(t => t.ApplyMigrations(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_PassesCancellationTokenThroughToTasks()
    {
        using var cts = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;

        var task = new Mock<IMigrationsTask>();
        task.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) =>
            {
                receivedToken = ct;
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton(task.Object);
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        await service.StartAsync(cts.Token);

        Assert.Equal(cts.Token, receivedToken);
    }

    [Fact]
    public async Task StartAsync_ResolvesTasksFromScope_ScopedLifetimeIsHonored()
    {
        var instancesCreated = 0;

        var services = new ServiceCollection();
        services.AddScoped<IMigrationsTask>(_ =>
        {
            Interlocked.Increment(ref instancesCreated);
            var mock = new Mock<IMigrationsTask>();
            mock.Setup(t => t.ApplyMigrations(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return mock.Object;
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        // A scoped IMigrationsTask can only be resolved because the service creates its own scope.
        // ValidateScopes=true would throw if it tried to resolve scoped services from the root provider.
        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        // A fresh scoped instance is created on each run (each StartAsync opens a new scope).
        Assert.Equal(2, instancesCreated);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        var services = new ServiceCollection();
        await using var provider = services.BuildServiceProvider();

        var service = CreateService(new MigrationsServiceSparkSettings { Enabled = true }, provider);

        await service.StopAsync(CancellationToken.None);
    }
}