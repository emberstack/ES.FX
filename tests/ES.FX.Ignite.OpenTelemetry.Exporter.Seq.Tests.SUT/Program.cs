using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT.Endpoints;
using ES.FX.Ignite.Serilog.Hosting;

var seqParams = args.FirstOrDefault(x =>
    x.StartsWith($"--{ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT.Program.Args.ConnectionStringArg}="));

if (seqParams == null) throw new ArgumentException("Seq connection string is required");
var seqConnectionString = seqParams.Split("=")[1];

var builder = WebApplication.CreateBuilder([]);

builder.Ignite();
builder.IgniteSeqOpenTelemetryExporter(SeqOpenTelemetryExporterSpark.ConfigurationSectionPath,
    x => { x.Enabled = true; }, x =>
    {
        x.IngestionEndpoint = seqConnectionString;
        x.HealthUrl = $"{seqConnectionString}/health";
    });

builder.IgniteSerilog();

var app = builder.Build();

app.Ignite();

var root = app
    .MapGroup(string.Empty);

SimpleEndpoint.MapRoutes(root);

app.Run();


app.Run();

// ReSharper disable once UnusedMember.Global
namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT
{
    public class Program
    {
        public static class Args
        {
            public const string ConnectionStringArg = "seq-connection-string";
        }
    }
}