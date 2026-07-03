using ES.FX.TransactionalOutbox.Delivery;
using ES.FX.TransactionalOutbox.Delivery.Actions;
using ES.FX.TransactionalOutbox.Delivery.Faults;

namespace ES.FX.TransactionalOutbox.Tests;

public class DefaultOutboxMessageFaultHandlerTests
{
    private static OutboxMessageFaultContext CreateContext(int deliveryAttempts) => new()
    {
        MessageContext = new OutboxMessageContext
        {
            MessageType = typeof(string),
            Message = "payload",
            DeliveryAttempts = deliveryAttempts,
            DeliveryFirstAttemptedAt = DateTimeOffset.UnixEpoch,
            DeliveryLastAttemptedAt = null
        },
        FaultException = new InvalidOperationException("boom")
    };

    private static async Task<RedeliverMessageAction> HandleAsync(int deliveryAttempts)
    {
        var handler = new DefaultOutboxMessageFaultHandler();
        var result = await handler.HandleAsync(CreateContext(deliveryAttempts));
        // The default handler always redelivers, never discards.
        return Assert.IsType<RedeliverMessageAction>(result.Action);
    }

    [Fact]
    public async Task HandleAsync_Always_Redelivers_Never_Discards()
    {
        var action = await HandleAsync(1);
        Assert.IsNotType<DiscardMessageAction>(action);
    }

    [Fact]
    public async Task HandleAsync_First_Attempt_Uses_Initial_10s_Backoff()
    {
        var action = await HandleAsync(1);
        Assert.Equal(TimeSpan.FromSeconds(10), action.Delay);
    }

    [Theory]
    [InlineData(1, 10)]     // 10 * 2^0
    [InlineData(2, 20)]     // 10 * 2^1
    [InlineData(3, 40)]     // 10 * 2^2
    [InlineData(4, 80)]     // 10 * 2^3
    [InlineData(5, 160)]    // 10 * 2^4
    public async Task HandleAsync_Applies_Exponential_Backoff(int attempts, double expectedSeconds)
    {
        var action = await HandleAsync(attempts);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), action.Delay);
    }

    [Fact]
    public async Task HandleAsync_Caps_Backoff_At_One_Hour()
    {
        // 10 * 2^19 far exceeds 3600 -> must clamp to 1 hour.
        var action = await HandleAsync(20);
        Assert.Equal(TimeSpan.FromHours(1), action.Delay);
        Assert.Equal(TimeSpan.FromSeconds(3600), action.Delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task HandleAsync_Treats_NonPositive_Attempts_As_First_Attempt(int attempts)
    {
        // Math.Max(1, attempts) => attempts <= 0 behaves like attempt 1 => 10s.
        var action = await HandleAsync(attempts);
        Assert.Equal(TimeSpan.FromSeconds(10), action.Delay);
    }

    [Fact]
    public async Task HandleAsync_Honors_Cancellation_Token_Without_Throwing()
    {
        // The default handler is purely computational; a cancelled token must not affect the result.
        var handler = new DefaultOutboxMessageFaultHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await handler.HandleAsync(CreateContext(1), cts.Token);

        var action = Assert.IsType<RedeliverMessageAction>(result.Action);
        Assert.Equal(TimeSpan.FromSeconds(10), action.Delay);
    }
}
