using System.Diagnostics;
using ES.FX.Migrations.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
///     <see cref="IMigrationsTask" /> for applying migrations to <see cref="DbContext" /> that uses relational databases.
/// </summary>
/// <typeparam name="TDbContext">The <see cref="DbContext" /> type</typeparam>
/// <param name="logger">Logger instance</param>
/// <param name="context">The <see cref="TDbContext" /> instance</param>
public partial class RelationalDbContextMigrationsTask<TDbContext>(
    ILogger<RelationalDbContextMigrationsTask<TDbContext>> logger,
    TDbContext context) : IMigrationsTask where TDbContext : DbContext
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        LogApplyingMigrations(typeof(TDbContext).Name);

        var start = Stopwatch.GetTimestamp();

        var migrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false)).ToArray();

        if (migrations.Length > 0)
        {
            LogApplyingMigrationCount(migrations.Length);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogNoMigrations();
        }

        var elapsed = Stopwatch.GetElapsedTime(start);
        LogMigrationsCompleted(typeof(TDbContext).Name, elapsed);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying migrations for {ContextType}")]
    private partial void LogApplyingMigrations(string contextType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} migrations")]
    private partial void LogApplyingMigrationCount(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "No migrations to apply")]
    private partial void LogNoMigrations();

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrations for {ContextType} completed in {Elapsed}")]
    private partial void LogMigrationsCompleted(string contextType, TimeSpan elapsed);
}