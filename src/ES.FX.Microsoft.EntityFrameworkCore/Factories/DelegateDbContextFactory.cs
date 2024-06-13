using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace ES.FX.Microsoft.EntityFrameworkCore.Factories;

/// <summary>
/// Defines a factory for creating instances of <see cref="DbContext"/> using a delegate.
/// </summary>
/// <typeparam name="T">DbContext type</typeparam>
/// <param name="serviceProvider">Service provider used by the factory</param>
/// <param name="factory">Factory function used to create the DbContext</param>
[PublicAPI]
public class DelegateDbContextFactory<T>(IServiceProvider serviceProvider, Func<IServiceProvider, T> factory)
    : IDbContextFactory<T>
    where T : DbContext
{
    public T CreateDbContext() => factory(serviceProvider);
}