using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Entities;

public class OutboxMessageFault
{
    /// <summary>
    ///     The message id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     The outbox id
    /// </summary>
    public Guid OutboxId { get; set; }

    /// <summary>
    ///     Time at which the message was added to the outbox
    /// </summary>
    public required DateTimeOffset AddedAt { get; set; }

    /// <summary>
    ///     Header collection serialized as JSON. The headers are used to store metadata about the message
    /// </summary>
    public required string Headers { get; set; }

    /// <summary>
    ///     The type of the payload (custom type value or assembly qualified name)
    /// </summary>
    public required string PayloadType { get; set; }

    /// <summary>
    ///     JSON serialized payload
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    ///     The number of delivery attempts
    /// </summary>
    public required int DeliveryAttempts { get; set; }

    /// <summary>
    ///     The time at which the message was first attempted to be delivered
    /// </summary>
    public required DateTimeOffset? DeliveryFirstAttemptedAt { get; set; }


    /// <summary>
    ///     The time at which the message was last attempted to be delivered
    /// </summary>
    public required DateTimeOffset? DeliveryLastAttemptedAt { get; set; }

    /// <summary>
    ///     The error message from the last delivery attempt. This is used to store the exception message or any other error
    ///     information
    /// </summary>
    public required string? DeliveryLastAttemptError { get; set; }

    /// <summary>
    ///     The time after which this message should be delivered. If this is null, the message will be delivered immediately
    /// </summary>
    public required DateTimeOffset? DeliveryNotBefore { get; set; }

    /// <summary>
    ///     The time before which this message should be delivered. If this time is reached, the message will be discarded
    /// </summary>
    public required DateTimeOffset? DeliveryNotAfter { get; set; }


    /// <summary>
    ///     The time at which the message delivery was considered permanently failed
    /// </summary>
    public required DateTimeOffset FaultedAt { get; set; }

    internal class EntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessageFault>
    {
        public void Configure(EntityTypeBuilder<OutboxMessageFault> builder)
        {
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasKey(p => p.Id);

            builder.Property(p => p.AddedAt);

            builder.Property(p => p.Headers);

            builder.Property(p => p.PayloadType);
            builder.Property(p => p.Payload);

            builder.Property(p => p.DeliveryAttempts);
            builder.Property(p => p.DeliveryFirstAttemptedAt);
            builder.Property(p => p.DeliveryLastAttemptedAt);
            builder.Property(p => p.DeliveryLastAttemptError).HasMaxLength(4000);

            builder.Property(p => p.DeliveryNotBefore);
            builder.Property(p => p.DeliveryNotAfter);

            builder.Property(p => p.FaultedAt);
        }
    }
}