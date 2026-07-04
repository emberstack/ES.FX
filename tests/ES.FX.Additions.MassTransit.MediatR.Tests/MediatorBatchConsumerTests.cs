using ES.FX.Additions.MassTransit.MediatR.Consumers;
using ES.FX.Additions.MediatR.Contracts.Batches;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.Additions.MassTransit.MediatR.Tests;

/// <summary>
///     Functional coverage of <see cref="MediatorBatchConsumer{TMessage}" /> driven through the
///     MassTransit in-memory test harness. Each consumer is configured with a fixed message limit so a
///     full batch forms deterministically without relying on a broker or wall-clock timing. A mocked
///     <see cref="IMediator" /> lets each test assert whether the batch was <c>Publish</c>ed (as a
///     <see cref="BatchNotification{T}" />) or <c>Send</c> (as a <see cref="BatchRequest{T}" />), and that
///     every element was carried through into <c>Items</c>.
/// </summary>
public class MediatorBatchConsumerTests
{
    private const int BatchSize = 3;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(ITestHarness Harness, Mock<IMediator> Mediator, ServiceProvider Provider)>
        StartHarnessAsync(Action<IBusRegistrationConfigurator> configureConsumers)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Loose);

        var provider = new ServiceCollection()
            .AddSingleton(mediator.Object)
            .AddMassTransitTestHarness(configureConsumers)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        return (harness, mediator, provider);
    }

    [Fact]
    public async Task Batch_consumer_publishes_batch_notification_when_element_type_is_a_notification()
    {
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorBatchConsumer<NotificationMessage>, NotificationBatchDefinition>());
        await using var _ = provider;

        var ids = Enumerable.Range(0, BatchSize).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
            await harness.Bus.Publish(new NotificationMessage(id), Ct);

        // Wait for all elements to be consumed as a batch.
        Assert.Equal(BatchSize, await CountConsumed<NotificationMessage>(harness));

        mediator.Verify(m => m.Publish(
                It.Is<BatchNotification<NotificationMessage>>(b =>
                    b.Items.Count == BatchSize &&
                    ids.All(id => b.Items.Any(i => i.Id == id))),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    [Fact]
    public async Task Batch_consumer_sends_batch_request_when_element_type_is_a_request()
    {
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorBatchConsumer<RequestMessage>, RequestBatchDefinition>());
        await using var _ = provider;

        var ids = Enumerable.Range(0, BatchSize).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
            await harness.Bus.Publish(new RequestMessage(id), Ct);

        Assert.Equal(BatchSize, await CountConsumed<RequestMessage>(harness));

        mediator.Verify(m => m.Send(
                It.Is<BatchRequest<RequestMessage>>(b =>
                    b.Items.Count == BatchSize &&
                    ids.All(id => b.Items.Any(i => i.Id == id))),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    [Fact]
    public async Task Batch_consumer_prefers_publish_over_send_for_dual_element_type()
    {
        // DualMessage is BOTH INotification and IRequest. The batch consumer gates on the STATIC
        // typeof(TMessage) generic and checks INotification first, so it must publish a BatchNotification.
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorBatchConsumer<DualMessage>, DualBatchDefinition>());
        await using var _ = provider;

        var ids = Enumerable.Range(0, BatchSize).Select(_ => Guid.NewGuid()).ToArray();
        foreach (var id in ids)
            await harness.Bus.Publish(new DualMessage(id), Ct);

        Assert.Equal(BatchSize, await CountConsumed<DualMessage>(harness));

        mediator.Verify(m => m.Publish(
                It.IsAny<BatchNotification<DualMessage>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    private static async Task<int> CountConsumed<T>(ITestHarness harness)
        where T : class
    {
        // Block until the harness reports at least BatchSize consumed messages of type T.
        Assert.True(await harness.Consumed.Any<T>(Ct));
        var count = 0;
        await foreach (var _ in harness.Consumed.SelectAsync<T>(Ct))
        {
            count++;
            if (count >= BatchSize) break;
        }

        return count;
    }

    // Batch consumer definitions: fix the message limit to BatchSize so a full batch forms as soon as
    // BatchSize messages arrive (with a generous time limit as a fallback safety net).
    private sealed class NotificationBatchDefinition : ConsumerDefinition<MediatorBatchConsumer<NotificationMessage>>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<MediatorBatchConsumer<NotificationMessage>> consumerConfigurator,
            IRegistrationContext context) =>
            consumerConfigurator.Options<BatchOptions>(options => options
                .SetMessageLimit(BatchSize)
                .SetTimeLimit(TimeSpan.FromSeconds(5)));
    }

    private sealed class RequestBatchDefinition : ConsumerDefinition<MediatorBatchConsumer<RequestMessage>>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<MediatorBatchConsumer<RequestMessage>> consumerConfigurator,
            IRegistrationContext context) =>
            consumerConfigurator.Options<BatchOptions>(options => options
                .SetMessageLimit(BatchSize)
                .SetTimeLimit(TimeSpan.FromSeconds(5)));
    }

    private sealed class DualBatchDefinition : ConsumerDefinition<MediatorBatchConsumer<DualMessage>>
    {
        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<MediatorBatchConsumer<DualMessage>> consumerConfigurator,
            IRegistrationContext context) =>
            consumerConfigurator.Options<BatchOptions>(options => options
                .SetMessageLimit(BatchSize)
                .SetTimeLimit(TimeSpan.FromSeconds(5)));
    }
}