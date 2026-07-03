using MediatR;

namespace ES.FX.Additions.MassTransit.MediatR.Tests;

// A message that is ONLY a MediatR notification.
public sealed record NotificationMessage(Guid Id) : INotification;

// A message that is ONLY a MediatR request (no response).
public sealed record RequestMessage(Guid Id) : IRequest;

// A message that is BOTH a notification AND a request. Used to prove the
// "publish wins over send" preference in both consumers.
public sealed record DualMessage(Guid Id) : INotification, IRequest;

// A message that is neither a notification nor a request. Exercises the
// unsupported/default path that throws InvalidOperationException.
public sealed record UnsupportedMessage(Guid Id);
