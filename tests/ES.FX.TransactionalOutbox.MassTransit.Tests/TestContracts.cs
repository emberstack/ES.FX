namespace ES.FX.TransactionalOutbox.MassTransit.Tests;

// Message contracts published through the outbox handler in the in-memory harness.
// Kept local to this test project so the harness owns the full lifecycle.

public sealed record OutboxOrderCreated(Guid Id, string Name);

public sealed record OutboxOrderShipped(Guid Id);

public interface IOutboxAccountOpened
{
    Guid Id { get; }
}

public sealed record OutboxAccountOpened(Guid Id) : IOutboxAccountOpened;
