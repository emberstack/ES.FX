using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Ignite.Migrations.Service;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Ignite.Migrations.Tests.Service;

public class MigrationsServiceTests
{
    [Fact]
    public async Task MigrationService_Settings_Check()
    {
        var loggerMock = new Mock<ILogger<MigrationsService>>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IMigrationsTask, TestMigrationTask>();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var configuration = new MigrationsServiceSparkSettings
        {
            Enabled = false
        };

        var migrationService = new MigrationsService(loggerMock.Object, configuration, serviceProvider);
        await migrationService.StartAsync(CancellationToken.None);

        var testMigration = serviceProvider.GetService<IMigrationsTask>() as TestMigrationTask;
        Assert.NotNull(testMigration);
        Assert.False(testMigration.ApplyMigrationsCalled);
    }

    [Fact]
    public async Task MigrationService_Apply()
    {
        var loggerMock = new Mock<ILogger<MigrationsService>>();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IMigrationsTask, TestMigrationTask>();

        var serviceProvider = serviceCollection.BuildServiceProvider();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var configuration = new MigrationsServiceSparkSettings
        {
            Enabled = true
        };

        var migrationService = new MigrationsService(loggerMock.Object, configuration, serviceProvider);
        await migrationService.StartAsync(CancellationToken.None);

        var testMigration = serviceProvider.GetService<IMigrationsTask>() as TestMigrationTask;
        Assert.NotNull(testMigration);
        Assert.True(testMigration.ApplyMigrationsCalled);
    }
}

internal class TestMigrationTask : IMigrationsTask
{
    public bool ApplyMigrationsCalled { get; private set; }

    Task IMigrationsTask.ApplyMigrations(CancellationToken cancellationToken)
    {
        ApplyMigrationsCalled = true;
        return Task.CompletedTask;
    }
}