using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.MassTransit.Delivery;
using global::MassTransit;
using global::MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

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
}
