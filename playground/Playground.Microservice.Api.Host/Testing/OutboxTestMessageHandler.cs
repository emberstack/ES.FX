using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestMessageHandler : INotificationHandler<OutboxTestMessage>
{
    public async Task Handle(OutboxTestMessage request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        throw new Exception("Something went wrong");
    }
}