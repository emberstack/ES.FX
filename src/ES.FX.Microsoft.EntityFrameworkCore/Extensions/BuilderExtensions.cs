using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.Microsoft.EntityFrameworkCore.Extensions;

public static class BuilderExtensions
{
    /// <summary>
    ///     Registers an <see cref="EntityConfigurationsFromAssembliesExtension" /> to the
    ///     <see cref="DbContextOptionsBuilder" />.
    ///     Provides a list of assemblies to scan for <see cref="IEntityTypeConfiguration{TEntity}" /> implementations.
    ///     Requires the model builder to be configured with  />
    /// </summary>
    /// <param name="builder">The <see cref="DbContextOptionsBuilder" /></param>
    /// <param name="assemblies">List of <see cref="Assembly" /> to scan</param>
    public static void WithEntityConfigurationsFromAssembliesExtension(this DbContextOptionsBuilder builder,
        params Assembly[] assemblies)
    {
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new EntityConfigurationsFromAssembliesExtension
                { Assemblies = assemblies });
    }


    /// <summary>
    ///     Applies the <see cref="IEntityTypeConfiguration{TEntity}" /> implementations from the assemblies registered in the
    ///     <see cref="EntityConfigurationsFromAssembliesExtension" /> to the <see cref="ModelBuilder" />.
    /// </summary>
    /// <param name="builder">The <see cref="ModelBuilder" /></param>
    /// <param name="options">
    ///     The <see cref="DbContextOptions" /> to load the
    ///     <see cref="EntityConfigurationsFromAssembliesExtension" />> from
    /// </param>
    public static void ApplyConfigurationsFromAssembliesExtension(this ModelBuilder builder, DbContextOptions options)
    {
        var extension = options.FindExtension<EntityConfigurationsFromAssembliesExtension>();
        if (extension is null) return;
        foreach (var assembly in extension.Assemblies) builder.ApplyConfigurationsFromAssembly(assembly);
    }
}