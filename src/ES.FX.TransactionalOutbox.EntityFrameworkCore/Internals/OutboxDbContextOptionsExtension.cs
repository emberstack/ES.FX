using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;

/// <summary>
///     Extension for configuring the Outbox on a DbContext.
/// </summary>
internal class OutboxDbContextOptionsExtension : IDbContextOptionsExtension
{
    public OutboxDbContextOptionsExtension(OutboxDbContextOptions options)
    {
        Info = new ExtensionInfo(this);
        OutboxDbContextOptions = options;
    }


    internal OutboxDbContextOptions OutboxDbContextOptions { get; }


    public void ApplyServices(IServiceCollection services)
    {
        services.AddSingleton<IInterceptor>(new OutboxDbContextInterceptor());
        services.AddSingleton(OutboxDbContextOptions);
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