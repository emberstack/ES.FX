using ES.FX.Messaging;
using MassTransit;

namespace ES.FX.Additions.MassTransit.Messaging;

/// <summary>
///     Default <see cref="MassTransit" /> implementation of <see cref="IMessenger" />
/// </summary>
/// <param name="publishEndpoint">The <see cref="IPublishEndpoint" /> used to send the <see cref="IMessage" /></param>
public class MassTransitMessenger(IPublishEndpoint publishEndpoint) : IMessenger
{
    public void Send(IMessage message, CancellationToken cancellationToken = default)
    {
        SendAsync(message, cancellationToken).Wait(cancellationToken);
    }

    public async Task SendAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(message as object, cancellationToken).ConfigureAwait(false);
    }
}