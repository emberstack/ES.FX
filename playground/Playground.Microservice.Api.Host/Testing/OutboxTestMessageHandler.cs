using System.Diagnostics;
using ES.FX.MessageBus;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestMessageHandler(ILogger<OutboxTestMessageHandler> logger) :
    IMessageHandler<OutboxTestMessage>,
    IMessageHandler<OutboxTestMessage2>
{
    public async Task Handle(IMessageContext<OutboxTestMessage> context, CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        logger.LogTrace($"Processing {message.GetType().Name}");
        await context.Publish(new OutboxTestMessage2 { SomeProp = "some other prop" }, cancellationToken);

        var activityId = Activity.Current;
        logger.LogInformation($"Activity ID: {activityId?.Id ?? "No Activity"}");
        logger.LogInformation($"Activity ParentId: {activityId?.ParentId ?? "No Activity"}");
        logger.LogInformation($"Activity RootId: {activityId?.RootId ?? "No Activity"}");
        await Task.CompletedTask;
    }

    public async Task Handle(IMessageContext<OutboxTestMessage2> context, CancellationToken cancellationToken = default)
    {
        var message = context.Message;
        logger.LogTrace($"Processing {message.GetType().Name}");
        var activityId = Activity.Current;
        logger.LogInformation($"Activity ID: {activityId?.Id ?? "No Activity"}");
        logger.LogInformation($"Activity ParentId: {activityId?.ParentId ?? "No Activity"}");
        logger.LogInformation($"Activity RootId: {activityId?.RootId ?? "No Activity"}");
        await Task.CompletedTask;
    }
}
