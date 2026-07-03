using ES.FX.TransactionalOutbox.Delivery.Actions;
using ES.FX.TransactionalOutbox.Delivery.Faults;

namespace ES.FX.TransactionalOutbox.Tests;

public class DeliveryFaultResultTests
{
    [Fact]
    public void Discard_Produces_DiscardMessageAction()
    {
        var result = DeliveryFaultResult.Discard();

        Assert.IsType<DiscardMessageAction>(result.Action);
        Assert.IsNotType<RedeliverMessageAction>(result.Action);
    }

    [Fact]
    public void Redeliver_Produces_RedeliverMessageAction_With_Supplied_Delay()
    {
        var delay = TimeSpan.FromMinutes(3);

        var result = DeliveryFaultResult.Redeliver(delay);

        var action = Assert.IsType<RedeliverMessageAction>(result.Action);
        Assert.Equal(delay, action.Delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(90)]
    [InlineData(3600)]
    public void Redeliver_Preserves_Arbitrary_Delay_Values(int seconds)
    {
        var delay = TimeSpan.FromSeconds(seconds);

        var result = DeliveryFaultResult.Redeliver(delay);

        var action = Assert.IsType<RedeliverMessageAction>(result.Action);
        Assert.Equal(delay, action.Delay);
    }

    [Fact]
    public void Redeliver_Allows_Zero_Delay()
    {
        var result = DeliveryFaultResult.Redeliver(TimeSpan.Zero);

        var action = Assert.IsType<RedeliverMessageAction>(result.Action);
        Assert.Equal(TimeSpan.Zero, action.Delay);
    }

    [Fact]
    public void Discard_And_Redeliver_Produce_Distinct_Action_Types()
    {
        var discard = DeliveryFaultResult.Discard();
        var redeliver = DeliveryFaultResult.Redeliver(TimeSpan.FromSeconds(5));

        Assert.NotEqual(discard.Action.GetType(), redeliver.Action.GetType());
    }
}
