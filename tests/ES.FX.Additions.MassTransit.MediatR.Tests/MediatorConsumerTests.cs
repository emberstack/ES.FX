using ES.FX.Additions.MassTransit.MediatR.Consumers;
using ES.FX.Additions.MediatR.Contracts.Batches;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.Additions.MassTransit.MediatR.Tests;

/// <summary>
///     Functional coverage of <see cref="MediatorConsumer{TMessage}" /> and
///     <see cref="MediatorBatchConsumer{TMessage}" /> driven through the MassTransit in-memory test
///     harness. A mocked <see cref="IMediator" /> is registered in DI so each test can assert whether the
///     consumed message was <c>Publish</c>ed (notification) or <c>Send</c> (request) — no real broker.
/// </summary>
public class MediatorConsumerTests
{
    /// <summary>
    ///     Spins up a MassTransit in-memory harness with the supplied consumer configuration and a mocked
    ///     <see cref="IMediator" />. Returns the started harness and the mock for assertions. Caller owns
    ///     disposing the harness (via <c>await using</c>).
    /// </summary>
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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Single_consumer_publishes_when_message_is_a_notification()
    {
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorConsumer<NotificationMessage>>());
        await using var _ = provider;

        var message = new NotificationMessage(Guid.NewGuid());
        await harness.Bus.Publish(message, Ct);

        Assert.True(await harness.Consumed.Any<NotificationMessage>(Ct));

        mediator.Verify(m => m.Publish(
                It.Is<INotification>(n => ((NotificationMessage)n).Id == message.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    [Fact]
    public async Task Single_consumer_sends_when_message_is_a_request()
    {
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorConsumer<RequestMessage>>());
        await using var _ = provider;

        var message = new RequestMessage(Guid.NewGuid());
        await harness.Bus.Publish(message, Ct);

        Assert.True(await harness.Consumed.Any<RequestMessage>(Ct));

        mediator.Verify(m => m.Send(
                It.Is<IRequest>(r => ((RequestMessage)r).Id == message.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    [Fact]
    public async Task Single_consumer_prefers_publish_over_send_for_dual_message()
    {
        // A message that is BOTH INotification and IRequest. The single consumer matches on the runtime
        // instance and the INotification case is listed first, so Publish must win.
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorConsumer<DualMessage>>());
        await using var _ = provider;

        var message = new DualMessage(Guid.NewGuid());
        await harness.Bus.Publish(message, Ct);

        Assert.True(await harness.Consumed.Any<DualMessage>(Ct));

        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }

    [Fact]
    public async Task Single_consumer_faults_when_message_is_neither_notification_nor_request()
    {
        var (harness, mediator, provider) = await StartHarnessAsync(cfg =>
            cfg.AddConsumer<MediatorConsumer<UnsupportedMessage>>());
        await using var _ = provider;

        await harness.Bus.Publish(new UnsupportedMessage(Guid.NewGuid()), Ct);

        // The consumer throws InvalidOperationException; MassTransit surfaces this as a Fault.
        Assert.True(await harness.Published.Any<Fault<UnsupportedMessage>>(Ct));

        mediator.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()), Times.Never);

        await harness.Stop(Ct);
    }
}
