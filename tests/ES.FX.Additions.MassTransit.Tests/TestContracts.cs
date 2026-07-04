using ES.FX.ComponentModel.DataAnnotations;

namespace ES.FX.Additions.MassTransit.Tests;

// Message contracts with stable [Kind] values used by the formatter and publish-filter tests.
// These are intentionally NOT registered with MessageKindProvider unless a specific test does so,
// because MessageKindProvider.Cache is process-global static state.

[Kind("order-created")]
public sealed record OrderCreated(Guid Id);

[Kind("order-shipped")]
public sealed record OrderShipped(Guid Id);

// A contract carrying BOTH a Kind and an explicit FaultKind.
[Kind("payment-requested")]
[FaultKind("payment-fault")]
public sealed record PaymentRequested(Guid Id);

// A contract with a Kind but no FaultKind (exercises the fault-fallback path).
[Kind("invoice-issued")]
public sealed record InvoiceIssued(Guid Id);

// A contract with no attributes at all (exercises base-formatter fallthrough).
public sealed record PlainMessage(Guid Id);

// Interface contract carrying a Kind (publish falls back to T when runtime type has none).
[Kind("account-opened")]
public interface IAccountOpened
{
    Guid Id { get; }
}

public sealed record AccountOpened(Guid Id) : IAccountOpened;