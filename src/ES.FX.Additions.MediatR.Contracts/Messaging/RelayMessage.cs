using ES.FX.Messaging;
using JetBrains.Annotations;
using MediatR;

namespace ES.FX.Additions.MediatR.Contracts.Messaging;

/// <summary>
///     Represents a request to relay a message via a messenger
/// </summary>
[PublicAPI]
public record RelayMessage : IRequest
{
    /// <summary>
    ///     The message to be relayed
    /// </summary>
    public required IMessage Message { get; init; }
}