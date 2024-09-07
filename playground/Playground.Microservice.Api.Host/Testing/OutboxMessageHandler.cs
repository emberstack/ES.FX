using ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxMessageHandler : IOutboxMessageHandler
{
    public ValueTask<bool> IsReadyAsync() => ValueTask.FromResult(true);

    public async ValueTask<bool> HandleAsync(OutboxMessageHandlerContext context,
        CancellationToken cancellationToken = default)
    {
        //await Task.Delay(Random.Shared.Next(10,300), cancellationToken);

        await Task.CompletedTask;


        return true;
    }
}