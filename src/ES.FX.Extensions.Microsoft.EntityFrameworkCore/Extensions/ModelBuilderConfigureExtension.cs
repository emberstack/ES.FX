using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Extensions.Microsoft.EntityFrameworkCore.Extensions;

/// <summary>
///     Custom extension to run <see cref="ModelBuilder" /> configuration functions.
///     This is useful when the DbContext is in a separate assembly
/// </summary>
public class ModelBuilderConfigureExtension : IDbContextOptionsExtension
{
    public ModelBuilderConfigureExtension(params Action<ModelBuilder, DbContextOptions>[] configureActions)
    {
        ConfigureActions = configureActions;
        Info = new ExtensionInfo(this);
    }

    public Action<ModelBuilder, DbContextOptions>[] ConfigureActions { get; }

    public void ApplyServices(IServiceCollection services)
    {
        //No services required
    }

    public DbContextOptionsExtensionInfo Info { get; }

    public void Validate(IDbContextOptions options)
    {
        // No-op. No validation required
    }


    public sealed class ExtensionInfo(IDbContextOptionsExtension extension)
        : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;
        public override string LogFragment => string.Empty;
        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;
    }
}