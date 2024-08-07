using ES.FX.Shared.Seq.Tests.Fixtures;
using Seq.Api;

namespace ES.FX.Shared.Seq.Tests;

public class SeqServerFixtureTests(SeqContainerFixture seqServerContainerFixture)
    : IClassFixture<SeqContainerFixture>
{
    [Fact]
    public async Task SeqContainer_CanConnectAsync()
    {
        SeqConnection client = new SeqConnection(seqServerContainerFixture.GetConnectionString());
        var events = await client.Events.ListAsync();
        Assert.NotNull(events);
        Assert.Empty(events);
    }
}