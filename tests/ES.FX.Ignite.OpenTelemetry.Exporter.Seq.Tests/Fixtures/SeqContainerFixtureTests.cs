using Seq.Api;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.Fixtures;

public class SeqContainerFixtureTests(SeqContainerFixture seqContainerFixture)
    : IClassFixture<SeqContainerFixture>
{
    [Fact]
    public async Task SeqContainer_CanConnectAsync()
    {
        var client = new SeqConnection(seqContainerFixture.GetConnectionString());
        var events = await client.Events.ListAsync();
        Assert.NotNull(events);
        Assert.Empty(events);
    }
}