using ES.FX.Migrations.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Migrations.Tests.Support;

/// <summary>
///     A minimal DI-driven runner built purely on the <see cref="IMigrationsTask" /> public abstraction. It resolves
///     every registered <see cref="IMigrationsTask" /> from the provider and applies them sequentially in registration
///     order, propagating the supplied <see cref="CancellationToken" />. Mirrors the contract the Ignite migrations
///     service relies on, so these tests guard the abstraction's real behavior without depending on Ignite.
/// </summary>
internal static class MigrationsTaskRunner
{
    public static async Task<int> RunAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        var tasks = provider.GetServices<IMigrationsTask>();
        var applied = 0;
        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await task.ApplyMigrations(cancellationToken);
            applied++;
        }

        return applied;
    }
}
