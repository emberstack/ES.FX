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
public class RelationalDbContextMigrationsTask<TDbContext>(
    ILogger<RelationalDbContextMigrationsTask<TDbContext>> logger,
    TDbContext context) : IMigrationsTask where TDbContext : DbContext
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying migrations for {contextType}", typeof(TDbContext).Name);

        var stopWatch = new Stopwatch();
        stopWatch.Start();


        var migrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)
            .ConfigureAwait(false)).ToList();

        if (migrations.Count > 0)
        {
            logger.LogInformation("Applying {count} migrations", migrations.Count);
            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("No migrations to apply");
        }

        stopWatch.Stop();
        logger.LogInformation("Migrations for {contextType} completed in {elapsed}", typeof(TDbContext).Name,
            stopWatch.Elapsed);
    }
}