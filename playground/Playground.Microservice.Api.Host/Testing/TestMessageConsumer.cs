using MassTransit;

namespace Playground.Microservice.Api.Host.Testing;

public class TestMessageConsumer(ILogger<TestMessageConsumer> logger):IConsumer<TestMessage>
{
    public async Task Consume(ConsumeContext<TestMessage> context)
    {
        logger.LogInformation("Received TestMessage with Id: {Id}", context.Message.Id);
        await Task.CompletedTask;
    }
}