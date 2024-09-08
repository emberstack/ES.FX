using MassTransit;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class MediatorConsumer<TMessage>(IMediator mediator) : IConsumer<TMessage> where TMessage : class
{
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        await mediator.Publish(context.Message);
    }
}

public class MediatorConsumerDefinition<TMessage> :
    ConsumerDefinition<MediatorConsumer<TMessage>> where TMessage : class
{
    public MediatorConsumerDefinition()
    {
        // override the default endpoint name, for whatever reason
        EndpointName = "ha-submit-order";

        // limit the number of messages consumed concurrently
        // this applies to the consumer only, not the endpoint
        ConcurrentMessageLimit = 4;
    }
}
