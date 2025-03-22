using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Extensions.Microsoft.EntityFrameworkCore.Factories;

/// <summary>
///     Defines a factory for creating instances of <see cref="DbContext" /> using a delegate.
/// </summary>
/// <typeparam name="TDbContext"><see cref="DbContext" />> type</typeparam>
/// <param name="serviceProvider">Service provider used by the factory</param>
/// <param name="factory">Factory function used to create the <see cref="TDbContext" /></param>
[PublicAPI]
public class DelegateDbContextFactory<TDbContext>(
    IServiceProvider serviceProvider,
    Func<IServiceProvider, TDbContext> factory)
    : IDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    public TDbContext CreateDbContext() => factory(serviceProvider);
}