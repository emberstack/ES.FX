using ES.FX.Messaging;
using JetBrains.Annotations;
using MediatR;

namespace ES.FX.Extensions.MediatR.Abstractions.Contracts;

/// <summary>
///     Represents a request to relay a message via a messenger
/// </summary>
[PublicAPI]
public record RelayMessage : IRequest
{
    public RelayMessage()
    {
    }

    public RelayMessage(IMessage message) => Message = message;
    public required IMessage Message { get; init; }
}