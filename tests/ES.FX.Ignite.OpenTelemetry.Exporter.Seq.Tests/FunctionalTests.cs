using System.Text;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.Fixtures;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT.Endpoints;
using Newtonsoft.Json;
using Seq.Api;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests;

public class FunctionalTests(SeqContainerFixture seqFixture) : IClassFixture<SeqContainerFixture>
{
    [Fact]
    public async Task EventsArePresentInSeq()
    {
        var name = "name";
        Assert.NotNull(seqFixture.WebApplicationFactory);

        var client = seqFixture.WebApplicationFactory.CreateClient();

        var response = await client.PostAsync(
            SimpleEndpoint.RoutePattern,
            new StringContent(
                JsonConvert.SerializeObject(new SimpleEndpoint.Request(name)),
                Encoding.UTF8, "application/json"));

        var resultContent = await response.Content.ReadAsStringAsync();

        // wait for the events to be processed
        await Task.Delay(5000);

        var seqClient = new SeqConnection(seqFixture.GetConnectionString());
        var events = await seqClient.Events.ListAsync(null, null, null, 100, null, null, true);

        Assert.NotEmpty(events);
        Assert.Contains(events, x => x.RenderedMessage.Contains(SimpleEndpoint.RoutePattern));
    }
}