using ES.FX.Shared.Seq.Tests.Fixtures;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.Hosting;
using Microsoft.AspNetCore.Builder;
using Seq.Api;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests
{

    public class FunctionalTests(SeqContainerFixture seqFixture)
        : IClassFixture<SeqContainerFixture>
    {
        [Fact]
        public async Task EventsArePresentInSeq()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.Ignite();
            builder.IgniteHealthChecksUi();
            builder.IgniteSeqOpenTelemetryExporter(SeqOpenTelemetryExporterSpark.ConfigurationSectionPath, (x) =>
            {
                x.Enabled = true;
            }, (x) =>
            {
                x.IngestionEndpoint = seqFixture.GetConnectionString();
                x.HealthUrl = seqFixture.GetConnectionString() + "/health";
            });

            builder.IgniteSerilog();

            var app = builder.Build();

            app.Ignite();
            app.IgniteHealthChecksUi();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable xUnit1030 // Do not call ConfigureAwait(false) in test method
            app.RunAsync().ConfigureAwait(false);
#pragma warning restore xUnit1030 // Do not call ConfigureAwait(false) in test method
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            // bad approach, but we need to wait for the app to start
            await Task.Delay(10000);


            SeqConnection client = new SeqConnection(seqFixture.GetConnectionString());
            var events = await client.Events.ListAsync();
            Assert.NotNull(events);
            Assert.NotEmpty(events);
        }

    }
}
