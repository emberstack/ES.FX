using System.Diagnostics;
using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.Ignite.Migrations.Service;

/// <summary>
/// Hosted service for running migrations tasks.
/// </summary>
/// <param name="logger"> The <see cref="ILogger{TCategoryName}"/>.</param>
/// <param name="settings"> The <see cref="MigrationsServiceSparkSettings"/>.</param>
/// <param name="serviceProvider"> The <see cref="IServiceProvider"/> used to look up the <see cref="IMigrationsTask"/> instances.</param>
public class MigrationsService(
    ILogger<MigrationsService> logger,
    MigrationsServiceSparkSettings settings,
    IServiceProvider serviceProvider)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Service enabled: {enabled}", settings.Enabled);

        if (!settings.Enabled)
        {
            return;
        }


        logger.LogTrace("Running migration tasks");

        var migrationTasks = serviceProvider.GetServices<IMigrationsTask>().ToList();
        logger.LogDebug("Migrations tasks: {migrationTaskCount}", migrationTasks.Count);

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        for (var index = 0; index < migrationTasks.Count; index++)
        {
            var task = migrationTasks[index];
            logger.LogTrace("Running task {currentMigrationTaskIndex}/{migrationTaskCount}",
                index + 1, migrationTasks.Count);

            var taskStopwatch = new Stopwatch();
            taskStopwatch.Start();

            await task.ApplyMigrations(cancellationToken);

            taskStopwatch.Stop();
            logger.LogDebug("Task {currentMigrationTaskIndex}/{migrationTaskCount} completed in {elapsed}",
                index + 1, migrationTasks.Count, taskStopwatch.Elapsed);
        }

        stopwatch.Stop();
        logger.LogInformation("Migrations tasks completed in {totalElapsed}", stopwatch.Elapsed);


        logger.LogDebug("Exit on complete: {exitOnComplete}", settings.ExitOnComplete);

        if (settings.ExitOnComplete)
        {
            logger.LogInformation("Exiting application on migrations completed");
            //Use this instead of hostApplicationLifetime.StopApplication(); to ensure that the application exits immediately and cleanly
            Environment.Exit(0);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}