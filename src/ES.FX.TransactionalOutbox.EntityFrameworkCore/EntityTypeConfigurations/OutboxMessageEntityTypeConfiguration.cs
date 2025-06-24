using ES.FX.TransactionalOutbox.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.EntityTypeConfigurations;

internal class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(p => p.Id);
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AddedAt);

        builder.Property(p => p.Headers);
        builder.Property(p => p.Payload);
        builder.Property(p => p.PayloadType);

        builder.Property(p => p.ActivityId).HasMaxLength(128);

        builder.Property(p => p.DeliveryAttempts);
        builder.Property(p => p.DeliveryMaxAttempts);
        builder.Property(p => p.DeliveryFirstAttemptedAt);
        builder.Property(p => p.DeliveryLastAttemptedAt);
        builder.Property(p => p.DeliveryLastAttemptError).HasMaxLength(4000);

        builder.Property(p => p.DeliveryAttemptDelay);
        builder.Property(p => p.DeliveryAttemptDelayIsExponential);


        builder.Property(p => p.DeliveryNotBefore);
        builder.Property(p => p.DeliveryNotAfter);

        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}