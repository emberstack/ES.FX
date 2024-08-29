using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;

public class Outbox
{
    /// <summary>
    ///     Outbox identifier
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    ///     The time the outbox was added to the database
    /// </summary>
    public required DateTimeOffset AddedAt { get; set; }

    /// <summary>
    ///     Lock to be used by providers that require exclusive access to the outbox and do not want to use database locks
    /// </summary>
    public Guid Lock { get; set; }

    /// <summary>
    ///     Time until which the delivery of the outbox message is delayed
    /// </summary>
    public DateTimeOffset? DeliveryDelayedUntil { get; set; }

    /// <summary>
    ///     Row version for optimistic concurrency
    /// </summary>
    public byte[]? RowVersion { get; set; }

    internal class EntityTypeConfiguration : IEntityTypeConfiguration<Outbox>
    {
        public void Configure(EntityTypeBuilder<Outbox> builder)
        {
            builder.Property(p => p.Id);
            builder.HasKey(p => p.Id);

            builder.Property(p => p.AddedAt);
            //This is required, otherwise Sql Server will escalate to page locks
            builder.HasIndex(p => p.AddedAt);


            builder.Property(p => p.DeliveryDelayedUntil);
            //This is required, otherwise Sql Server will escalate to page locks
            builder.HasIndex(p => p.DeliveryDelayedUntil);

            builder.Property(p => p.Lock);

            builder.Property(p => p.RowVersion).IsRowVersion();
        }
    }
}