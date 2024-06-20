using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestMessageBatchHandler : IRequestHandler<BatchRequest<OutboxTestMessage>>
{
    public async Task Handle(BatchRequest<OutboxTestMessage> request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}