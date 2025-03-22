using ES.FX.Extensions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace ES.FX.Extensions.Serilog.Tests.Lifetime;

public class ProgramEntrySerilogExtensionsTests
{
    [Fact]
    public async Task Serilog_Used()
    {
        await ProgramEntry.CreateBuilder([]).UseSerilog(LogEventLevel.Verbose,
                config => { config.WriteTo.InMemory(); })
            .Build()
            .RunAsync(_ =>
            {
                Assert.NotEmpty(InMemorySink.Instance.LogEvents);
                return Task.FromResult(1);
            });
    }
}