using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.SUT;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Tests.Fixtures;

public class SeqSutFactory(string seqConnectionString) : WebApplicationFactory<Program>
{
    public string SeqConnectionString { get; } = seqConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(Program.Args.ConnectionStringArg, SeqConnectionString);
    }
}