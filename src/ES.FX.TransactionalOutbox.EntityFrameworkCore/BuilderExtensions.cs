using ES.FX.TransactionalOutbox.Entities;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.EntityTypeConfigurations;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore;

[PublicAPI]
public static class BuilderExtensions
{
    /// <summary>
    ///     Configures the <see cref="DbContext" /> to support the outbox pattern.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    public static void UseOutbox(this DbContextOptionsBuilder optionsBuilder,
        Action<OutboxDbContextOptions>? configureOptions = null)
    {
        OutboxDbContextOptions outboxDbContextOptions;
        var outboxDbContextOptionsExtension = optionsBuilder.Options.FindExtension<OutboxDbContextOptionsExtension>();
        if (outboxDbContextOptionsExtension is not null)
        {
            outboxDbContextOptions = outboxDbContextOptionsExtension.OutboxDbContextOptions;
        }
        else
        {
            outboxDbContextOptions = new OutboxDbContextOptions();
            outboxDbContextOptionsExtension = new OutboxDbContextOptionsExtension(outboxDbContextOptions);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
                .AddOrUpdateExtension(outboxDbContextOptionsExtension);
        }

        configureOptions?.Invoke(outboxDbContextOptions);
    }


    /// <summary>
    ///     Adds the required Outbox entities to the model builder
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder" /> to configure </param>
    public static void AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Outbox>().ToTable("__Outboxes");
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());

        modelBuilder.Entity<OutboxMessage>().ToTable("__OutboxMessages");
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityTypeConfiguration());
    }
}