using ES.FX.MediatR.Abstractions.Contracts;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestMessageHandler : IRequestHandler<OutboxTestMessage>
{
    public async Task Handle(OutboxTestMessage request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}