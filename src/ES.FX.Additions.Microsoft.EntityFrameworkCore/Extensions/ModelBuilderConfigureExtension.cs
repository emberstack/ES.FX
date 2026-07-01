using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;

/// <summary>
///     Custom extension to run <see cref="ModelBuilder" /> configuration functions.
///     This is useful when the DbContext is in a separate assembly
/// </summary>
/// <remarks>
///     <para>
///         ⚠️ <b>Footgun — the EF Core model is cached per <c>DbContext</c> type, not per options instance.</b>
///         <see cref="ExtensionInfo" /> reports a service-provider hash code of <c>0</c>
///         (<see cref="ExtensionInfo.GetServiceProviderHashCode" />) and
///         <see cref="ExtensionInfo.ShouldUseSameServiceProvider" /> always returns <see langword="true" />. As a
///         result, two options instances of the <em>same</em> <c>DbContext</c> type that carry
///         <em>different</em> <see cref="ConfigureActions" /> are treated as equivalent by EF Core and share a single
///         cached model. Whichever set of actions builds the model first wins; the second instance's actions are
///         silently never run.
///     </para>
///     <para>
///         If you need per-instance model variation (for example, the same <c>DbContext</c> type configured with
///         different mappings in different scopes), supply a custom
///         <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory" /> that incorporates the
///         registered <see cref="ConfigureActions" /> (or another discriminator) into the model cache key, so EF Core
///         builds and caches a distinct model per configuration.
///     </para>
/// </remarks>
public class ModelBuilderConfigureExtension : IDbContextOptionsExtension
{
    public ModelBuilderConfigureExtension(params Action<ModelBuilder, DbContextOptions>[] configureActions)
    {
        ArgumentNullException.ThrowIfNull(configureActions);

        ConfigureActions = configureActions;
        Info = new ExtensionInfo(this);
    }

    public IReadOnlyList<Action<ModelBuilder, DbContextOptions>> ConfigureActions { get; }

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