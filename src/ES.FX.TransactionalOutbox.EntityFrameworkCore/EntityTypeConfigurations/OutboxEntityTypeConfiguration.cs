using ES.FX.TransactionalOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.EntityTypeConfigurations;

internal class OutboxEntityTypeConfiguration : IEntityTypeConfiguration<Outbox>
{
    public void Configure(EntityTypeBuilder<Outbox> builder)
    {
        builder.Property(p => p.Id);
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AddedAt);
        builder.Property(p => p.DeliveryDelayedUntil);
        builder.Property(p => p.Lock);

        builder.Property(p => p.RowVersion).IsRowVersion();

        //This is required, otherwise Sql Server will escalate to page locks
        builder.HasIndex(p => p.AddedAt);
        builder.HasIndex(p => p.DeliveryDelayedUntil);
        builder.HasIndex(p => p.Lock);
    }
}