using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT.Endpoints;
using ES.FX.Ignite.Serilog.Hosting;

public class Program
{
    public static readonly string ConnectionStringArgument = "seq-connection-string";

    private static void Main(string[] args)
    {
        var seqParams = args.FirstOrDefault(x => x.StartsWith($"--{ConnectionStringArgument}="));
        if (seqParams == null) throw new ArgumentException("Seq connection string is required");
        var seqConnectionString = seqParams?.Split("=")[1];

        var builder = WebApplication.CreateBuilder([]);

        builder.Ignite();
        builder.IgniteHealthChecksUi();
        builder.IgniteSeqOpenTelemetryExporter(SeqOpenTelemetryExporterSpark.ConfigurationSectionPath,
            x => { x.Enabled = true; }, x =>
            {
                x.IngestionEndpoint = seqConnectionString;
                x.HealthUrl = seqConnectionString + "/health";
            });

        builder.IgniteSerilog();

        var app = builder.Build();

        app.Ignite();
        app.IgniteHealthChecksUi();

        var root = app
            .MapGroup(string.Empty);

        SimpleEndpoint.MapRoutes(root);

        app.Run();
    }
}

// ReSharper disable once UnusedMember.Global
namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT
{
    public class Program
    {
    }
}