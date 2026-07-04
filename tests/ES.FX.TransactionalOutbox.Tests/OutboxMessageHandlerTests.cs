using ES.FX.TransactionalOutbox.Delivery;

namespace ES.FX.TransactionalOutbox.Tests;

public class OutboxMessageHandlerTests
{
    [Fact]
    public async Task IsReadyAsync_Default_Implementation_Returns_True()
    {
        IOutboxMessageHandler handler = new DefaultsOnlyHandler();

        var ready = await handler.IsReadyAsync(TestContext.Current.CancellationToken);

        Assert.True(ready);
    }

    [Fact]
    public async Task IsReadyAsync_Default_Implementation_Returns_True_Even_When_Token_Cancelled()
    {
        IOutboxMessageHandler handler = new DefaultsOnlyHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The default readiness gate is a constant true and ignores cancellation.
        var ready = await handler.IsReadyAsync(cts.Token);

        Assert.True(ready);
    }

    [Fact]
    public async Task IsReadyAsync_Can_Be_Overridden_To_Gate_Delivery()
    {
        IOutboxMessageHandler handler = new NotReadyHandler();

        var ready = await handler.IsReadyAsync(TestContext.Current.CancellationToken);

        Assert.False(ready);
    }

    /// <summary>
    ///     A handler that relies entirely on the default interface members and only implements the required
    ///     <see cref="IOutboxMessageHandler.HandleAsync" />. It does NOT override <c>IsReadyAsync</c>, so the
    ///     default interface method is exercised.
    /// </summary>
    private sealed class DefaultsOnlyHandler : IOutboxMessageHandler
    {
        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    /// <summary>
    ///     A handler that overrides the default readiness gate to prove consumers can opt out of delivery.
    /// </summary>
    private sealed class NotReadyHandler : IOutboxMessageHandler
    {
        public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }
}