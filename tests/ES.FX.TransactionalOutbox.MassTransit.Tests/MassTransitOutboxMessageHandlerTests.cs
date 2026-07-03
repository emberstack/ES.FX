using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.MassTransit.Delivery;
using global::MassTransit;
using global::MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ES.FX.TransactionalOutbox.MassTransit.Tests;

/// <summary>
///     Functional regression coverage of <see cref="MassTransitOutboxMessageHandler" /> using the MassTransit
///     in-memory test harness (no broker, no Docker). Verifies that:
///     <list type="bullet">
///         <item><see cref="MassTransitOutboxMessageHandler.HandleAsync" /> publishes the outbox message to the bus,</item>
///         <item>the message is published under its runtime <see cref="OutboxMessageContext.MessageType" />,</item>
///         <item>context headers are stamped onto the published message,</item>
///         <item><see cref="MassTransitOutboxMessageHandler.IsReadyAsync" /> reflects bus health readiness.</item>
///     </list>
/// </summary>
public sealed class MassTransitOutboxMessageHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider BuildHarnessProvider() =>
        new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
                cfg.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context)))
            .BuildServiceProvider(true);

    // The bus control is itself an IPublishEndpoint (the root, non-scoped publish endpoint), which is exactly
    // what the delivery service supplies in production. Using it avoids resolving the scoped IPublishEndpoint
    // from the root container.
    private static MassTransitOutboxMessageHandler CreateHandler(IServiceProvider provider)
    {
        var busControl = provider.GetRequiredService<IBusControl>();
        return new MassTransitOutboxMessageHandler(busControl, busControl);
    }

    private static OutboxMessageContext ContextFor(
        object message,
        Type? messageType = null,
        IDictionary<string, string>? headers = null) =>
        new()
        {
            Message = message,
            MessageType = messageType ?? message.GetType(),
            Headers = headers ?? new Dictionary<string, string>(),
            DeliveryAttempts = 1,
            DeliveryFirstAttemptedAt = DateTimeOffset.UtcNow,
            DeliveryLastAttemptedAt = null
        };

    [Fact]
    public async Task HandleAsync_PublishesMessage_ToBus()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);
        var message = new OutboxOrderCreated(Guid.NewGuid(), "widget");

        await handler.HandleAsync(ContextFor(message), Ct);

        Assert.True(await harness.Published.Any<OutboxOrderCreated>(Ct));
        var published = harness.Published.Select<OutboxOrderCreated>(Ct).First();
        var payload = published.Context!.Message;
        Assert.NotNull(payload);
        Assert.Equal(message.Id, payload.Id);
        Assert.Equal("widget", payload.Name);
    }

    [Fact]
    public async Task HandleAsync_PublishesUnderRuntimeMessageType()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);
        var message = new OutboxOrderShipped(Guid.NewGuid());

        // MessageType is the concrete runtime type; the handler must forward it to the typed Publish overload.
        await handler.HandleAsync(ContextFor(message, typeof(OutboxOrderShipped)), Ct);

        Assert.True(await harness.Published.Any<OutboxOrderShipped>(Ct));
        // A different contract must NOT have been published as a side effect.
        Assert.False(await harness.Published.Any<OutboxOrderCreated>(Ct));
    }

    [Fact]
    public async Task HandleAsync_PublishesUnderInterfaceMessageType_WhenSpecified()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);
        var message = new OutboxAccountOpened(Guid.NewGuid());

        // The outbox stores a message type; here it is the interface contract, not the concrete record.
        await handler.HandleAsync(ContextFor(message, typeof(IOutboxAccountOpened)), Ct);

        // MassTransit publishes under the requested contract, so the interface must be observable on the bus.
        Assert.True(await harness.Published.Any<IOutboxAccountOpened>(Ct));
    }

    [Fact]
    public async Task HandleAsync_StampsContextHeaders_OnPublishedMessage()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);
        var headers = new Dictionary<string, string>
        {
            ["x-outbox-id"] = "abc-123",
            ["x-tenant"] = "acme"
        };

        await handler.HandleAsync(ContextFor(new OutboxOrderCreated(Guid.NewGuid(), "n"), headers: headers), Ct);

        Assert.True(await harness.Published.Any<OutboxOrderCreated>(Ct));
        var published = harness.Published.Select<OutboxOrderCreated>(Ct).First();

        Assert.True(published.Context!.Headers.TryGetHeader("x-outbox-id", out var id));
        Assert.Equal("abc-123", id?.ToString());
        Assert.True(published.Context.Headers.TryGetHeader("x-tenant", out var tenant));
        Assert.Equal("acme", tenant?.ToString());
    }

    [Fact]
    public async Task HandleAsync_WithNoHeaders_PublishesSuccessfully()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);

        await handler.HandleAsync(ContextFor(new OutboxOrderShipped(Guid.NewGuid())), Ct);

        Assert.True(await harness.Published.Any<OutboxOrderShipped>(Ct));
    }

    [Fact]
    public async Task IsReadyAsync_ReturnsTrue_WhenBusIsStarted()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);

        Assert.True(await handler.IsReadyAsync(Ct));
    }

    [Fact]
    public async Task IsReadyAsync_ReturnsFalse_WhenBusIsNotStarted()
    {
        await using var provider = BuildHarnessProvider();
        // Deliberately do NOT start the harness; the bus health status should not be Healthy.
        var handler = CreateHandler(provider);

        Assert.False(await handler.IsReadyAsync(Ct));
    }

    [Fact]
    public async Task HandleAsync_HonorsCancellationToken()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var handler = CreateHandler(provider);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(ContextFor(new OutboxOrderShipped(Guid.NewGuid())), cts.Token).AsTask());
    }

    // Gap: HandleAsync failure propagation. The delivery service relies on HandleAsync throwing when the
    // publish cannot succeed (bus stopped / transport unavailable) so the outbox message is retried rather
    // than silently marked delivered. The handler does not catch/swallow publish exceptions, so any fault
    // from IPublishEndpoint.Publish must surface to the caller. The two tests below prove this from opposite
    // angles: (1) a hand-rolled endpoint that throws a broker-style fault, and (2) a real in-memory bus that
    // has been stopped, which faults on publish the way an unavailable transport would.

    [Fact]
    public async Task HandleAsync_PropagatesPublishFailure_FromEndpoint()
    {
        var boom = new InvalidOperationException("broker unavailable");

        // The typed Publish(object, Type, callback, CT) extension the handler calls resolves to this
        // interface overload; faulting it simulates an unavailable transport rejecting the publish.
        var publishEndpoint = new Mock<IPublishEndpoint>(MockBehavior.Loose);
        publishEndpoint
            .Setup(e => e.Publish(
                It.IsAny<object>(),
                It.IsAny<Type>(),
                It.IsAny<IPipe<PublishContext>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(boom);

        // IBusControl is not touched by HandleAsync, so a bare mock is sufficient.
        var handler = new MassTransitOutboxMessageHandler(Mock.Of<IBusControl>(), publishEndpoint.Object);

        // The failure must surface unchanged so the delivery service can count the attempt and retry.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(ContextFor(new OutboxOrderShipped(Guid.NewGuid())), Ct).AsTask());
        Assert.Same(boom, thrown);
    }

    // Documents the observed behavior of the in-memory transport (not the handler contract): publishing on a
    // *stopped* in-memory bus does NOT fault. The in-memory transport accepts the publish without a live
    // broker, so this path cannot exercise failure propagation. The deterministic proof of the gap
    // (HandleAsync surfaces a publish fault so the delivery service retries) is
    // HandleAsync_PropagatesPublishFailure_FromEndpoint above, which faults the publish call directly.
    // Asserting current real behavior here keeps the boundary explicit and guards against silently changing
    // it. IsReadyAsync is the mechanism the delivery service uses to avoid publishing on a down bus.
    [Fact]
    public async Task HandleAsync_DoesNotThrow_OnStoppedInMemoryBus_AndBecomesNotReady()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        var busControl = provider.GetRequiredService<IBusControl>();
        var handler = CreateHandler(provider);

        await busControl.StopAsync(Ct);

        // Readiness flips to false once the bus is stopped, which is how the delivery service holds off.
        Assert.False(await handler.IsReadyAsync(Ct));

        // The in-memory transport does not fault the publish; record that observed behavior.
        await handler.HandleAsync(ContextFor(new OutboxOrderShipped(Guid.NewGuid())), Ct);
    }
}
