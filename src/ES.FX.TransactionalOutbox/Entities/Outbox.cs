namespace ES.FX.TransactionalOutbox.Entities;

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
    public Guid? Lock { get; set; }

    /// <summary>
    ///     Time until which the delivery of the outbox message is delayed
    /// </summary>
    public DateTimeOffset? DeliveryDelayedUntil { get; set; }

    /// <summary>
    ///     Row version for optimistic concurrency
    /// </summary>
    public byte[]? RowVersion { get; set; }
}