using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;
using MassTransit;

namespace Playground.Microservice.Api.Host.Testing;

public class MassTransitOutboxRelay(IBusControl busControl, IPublishEndpoint publishEndpoint) : IOutboxMessageHandler
{
    public async ValueTask<bool> IsReadyAsync() =>
        await busControl.WaitForHealthStatus(BusHealthStatus.Healthy, TimeSpan.FromSeconds(5)).ConfigureAwait(false) ==
        BusHealthStatus.Healthy;

    public async ValueTask<bool> HandleAsync(OutboxMessageHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(context.Message, cancellationToken);
        return true;
    }
}