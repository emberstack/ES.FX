using ES.FX.Messaging;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Extensions.MassTransit.Messaging;

/// <summary>
///     Message handler that uses a <see cref="IPublishEndpoint" /> to publish <see cref="IMessage" />
/// </summary>
/// <param name="busControl">The <see cref="IBusControl" /> used to check if the handler can publish messages</param>
/// <param name="publishEndpoint">The endpoint used to publish messages</param>
[PublicAPI]
public class MassTransitMessageHandler(IBusControl busControl, IPublishEndpoint publishEndpoint)
    : IMessageHandler
{
    public async ValueTask<bool> HandleAsync(IMessage message,
        CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(message as object, cancellationToken);
        return true;
    }

    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellation = default) =>
        await busControl.WaitForHealthStatus(BusHealthStatus.Healthy, cancellation).ConfigureAwait(false) ==
        BusHealthStatus.Healthy;
}