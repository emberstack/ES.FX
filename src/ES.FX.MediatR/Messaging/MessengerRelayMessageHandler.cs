using ES.FX.MediatR.Abstractions.Contracts;
using ES.FX.Messaging;
using MediatR;

namespace ES.FX.MediatR.Messaging;
/// <summary>
/// Represents a <see cref="IRequestHandler{RelayMessage}"/> that sends the <see cref="IMessage"/> to the <see cref="IMessenger"/> supplied
/// </summary>
/// <param name="messenger">The <see cref="IMessenger"/> used to send the <see cref="IMessage"/></param>
public class MessengerRelayMessageHandler(IMessenger messenger):IRequestHandler<RelayMessage>
{
    public async Task Handle(RelayMessage request, CancellationToken cancellationToken)
    {
        await messenger.SendAsync(request.Message, cancellationToken).ConfigureAwait(false);
    }
}