using MassTransit;
using MediatR;

namespace Playground.Microservice.Api.Host.Testing;

public class MediatorGenericConsumer<TMessage>(IMediator mediator) : IConsumer<TMessage> where TMessage : class
{
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        await mediator.Publish(context.Message);
    }
}
