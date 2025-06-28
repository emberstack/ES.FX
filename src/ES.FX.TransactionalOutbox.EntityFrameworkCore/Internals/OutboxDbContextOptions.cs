using ES.FX.TransactionalOutbox.Serialization;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;

/// <summary>
///     Configuration options for the Outbox extension on the DbContext.
/// </summary>
public class OutboxDbContextOptions
{
    /// <summary>
    ///     Serializer used to serialize and deserialize outbox messages.
    /// </summary>
    public IOutboxSerializer Serializer { get; set; } = new DefaultOutboxSerializer(new DefaultPayloadTypeResolver());
}