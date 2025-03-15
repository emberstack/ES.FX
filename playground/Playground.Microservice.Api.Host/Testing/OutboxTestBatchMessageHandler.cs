using ES.FX.MediatR.Contracts;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestBatchMessageHandler : INotificationHandler<BatchNotification<OutboxTestMessage>>
{
    public async Task Handle(BatchNotification<OutboxTestMessage> notification, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}