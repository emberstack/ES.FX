using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Microsoft.EntityFrameworkCore.Extensions;

/// <summary>
///     Custom extension to load <see cref="IEntityTypeConfiguration{TEntity}" /> implementations from assemblies.
///     This is useful when the DbContext is in a separate assembly from the entity configurations and cannot reference the
///     entity configurations assemblies directly.
/// </summary>
public class EntityConfigurationsFromAssembliesExtension : IDbContextOptionsExtension
{
    public EntityConfigurationsFromAssembliesExtension() => Info = new ExtensionInfo(this);
    public required Assembly[] Assemblies { get; init; }

    public void ApplyServices(IServiceCollection services)
    {
        //No services required
    }

    public void Validate(IDbContextOptions options)
    {
        // No-op. No validation required
    }

    public DbContextOptionsExtensionInfo Info { get; }


    public sealed class ExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => string.Empty;
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;
        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }
    }
}