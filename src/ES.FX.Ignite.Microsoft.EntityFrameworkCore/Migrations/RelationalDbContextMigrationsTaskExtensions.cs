using ES.FX.Migrations.Abstractions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;


[PublicAPI]
public static class RelationalDbContextMigrationsTaskExtensions
{
    /// <summary>
    /// Registers a <see cref="IMigrationsTask"/> for applying migrations to <see cref="TDbContext"/> that uses relational databases.
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    /// <param name="builder"></param>
    public static void AddDbContextMigrationsTask<TDbContext>(this IHostApplicationBuilder builder) where TDbContext : DbContext
    {
        builder.Services.AddTransient<IMigrationsTask, RelationalDbContextMigrationsTask<TDbContext>>();
    }
}