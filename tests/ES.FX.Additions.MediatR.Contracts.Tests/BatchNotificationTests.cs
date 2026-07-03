using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

namespace ES.FX.Additions.MediatR.Contracts.Tests;

public class BatchNotificationTests
{
    public sealed record Event(string Kind);

    [Fact]
    public void BatchNotification_ImplementsINotification()
    {
        Assert.IsAssignableFrom<INotification>(new BatchNotification<int>());
    }

    [Fact]
    public void BatchNotification_DoesNotImplementIRequest()
    {
        // Guards the contract boundary: a notification must not be a request.
        Assert.IsNotAssignableFrom<IBaseRequest>(new BatchNotification<int>());
    }

    [Fact]
    public void Items_DefaultsToEmptyNonNullList()
    {
        var notification = new BatchNotification<string>();

        Assert.NotNull(notification.Items);
        Assert.Empty(notification.Items);
    }

    [Fact]
    public void Items_RoundTripsPayload()
    {
        var events = new List<Event> { new("created"), new("updated"), new("deleted") };

        var notification = new BatchNotification<Event> { Items = events };

        Assert.Equal(3, notification.Items.Count);
        Assert.Equal(events[0], notification.Items[0]);
        Assert.Equal(events[2], notification.Items[2]);
        Assert.IsAssignableFrom<IReadOnlyList<Event>>(notification.Items);
    }

    [Fact]
    public void WithExpression_ProducesNewInstance_LeavingOriginalUnchanged()
    {
        var original = new BatchNotification<string> { Items = ["a", "b"] };
        var updated = original with { Items = ["c"] };

        Assert.Equal(["a", "b"], original.Items);
        Assert.Equal(["c"], updated.Items);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public async Task SatisfiesNotificationHandlerConstraint_AndHandlerReceivesItems()
    {
        // INotificationHandler<TNotification> has `where TNotification : INotification`.
        // FirstHandler compiling proves BatchNotification<string> satisfies it; here we
        // exercise the handler to confirm Items flow through unchanged.
        FirstHandler.LastSeenItems = [];
        var handler = new FirstHandler();
        var notification = new BatchNotification<string> { Items = ["x", "y"] };

        await handler.Handle(notification, TestContext.Current.CancellationToken);

        Assert.Equal(["x", "y"], FirstHandler.LastSeenItems);
    }

    [Fact]
    public void ClosedGeneric_ImplementsINotificationInterface_ViaReflection()
    {
        var iface = typeof(BatchNotification<string>).GetInterfaces();

        Assert.Contains(iface, i => i == typeof(INotification));
        Assert.DoesNotContain(iface, i => i == typeof(IBaseRequest));
    }

    // where TNotification : INotification — will not compile if the contract regresses.
    private sealed class FirstHandler : INotificationHandler<BatchNotification<string>>
    {
        public static IReadOnlyList<string> LastSeenItems { get; set; } = [];

        public Task Handle(BatchNotification<string> notification, CancellationToken cancellationToken)
        {
            LastSeenItems = notification.Items;
            return Task.CompletedTask;
        }
    }
}
