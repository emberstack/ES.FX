using System.Diagnostics;
using ES.FX.Ignite.Migrations.Configuration;
using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.Ignite.Migrations.Service;

/// <summary>
///     Hosted service for running migrations tasks.
/// </summary>
/// <param name="logger"> The <see cref="ILogger{TCategoryName}" />.</param>
/// <param name="settings"> The <see cref="MigrationsServiceSparkSettings" />.</param>
/// <param name="serviceProvider">
///     The <see cref="IServiceProvider" /> used to look up the <see cref="IMigrationsTask" />
///     instances.
/// </param>
public partial class MigrationsService(
    ILogger<MigrationsService> logger,
    MigrationsServiceSparkSettings settings,
    IServiceProvider serviceProvider)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        LogServiceEnabled(settings.Enabled);

        if (!settings.Enabled) return;


        LogRunningMigrationTasks();

        await using var scope = serviceProvider.CreateAsyncScope();

        var migrationTasks = scope.ServiceProvider.GetServices<IMigrationsTask>().ToList();
        LogMigrationTaskCount(migrationTasks.Count);

        var startTimestamp = Stopwatch.GetTimestamp();

        for (var index = 0; index < migrationTasks.Count; index++)
        {
            var task = migrationTasks[index];
            LogRunningTask(index + 1, migrationTasks.Count);

            var taskStartTimestamp = Stopwatch.GetTimestamp();

            await task.ApplyMigrations(cancellationToken).ConfigureAwait(false);

            LogTaskCompleted(index + 1, migrationTasks.Count, Stopwatch.GetElapsedTime(taskStartTimestamp));
        }

        LogMigrationTasksCompleted(Stopwatch.GetElapsedTime(startTimestamp));


        LogExitOnComplete(settings.ExitOnComplete);

        if (settings.ExitOnComplete)
        {
            LogExitingApplication();
            //Use this instead of hostApplicationLifetime.StopApplication(); to ensure that the application exits immediately and cleanly
            Environment.Exit(0);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Service enabled: {enabled}")]
    partial void LogServiceEnabled(bool enabled);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Running migration tasks")]
    partial void LogRunningMigrationTasks();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Migrations tasks: {migrationTaskCount}")]
    partial void LogMigrationTaskCount(int migrationTaskCount);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Running task {currentMigrationTaskIndex}/{migrationTaskCount}")]
    partial void LogRunningTask(int currentMigrationTaskIndex, int migrationTaskCount);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Task {currentMigrationTaskIndex}/{migrationTaskCount} completed in {elapsed}")]
    partial void LogTaskCompleted(int currentMigrationTaskIndex, int migrationTaskCount, TimeSpan elapsed);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations tasks completed in {totalElapsed}")]
    partial void LogMigrationTasksCompleted(TimeSpan totalElapsed);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Exit on complete: {exitOnComplete}")]
    partial void LogExitOnComplete(bool exitOnComplete);

    [LoggerMessage(Level = LogLevel.Information, Message = "Exiting application on migrations completed")]
    partial void LogExitingApplication();
}