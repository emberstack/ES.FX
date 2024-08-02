using ES.FX.Hosting.Lifetime;
using ES.FX.Serilog.Lifetime;
using Microsoft.Extensions.Hosting;
using Moq;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace ES.FX.Serilog.Tests.Lifetime
{
    public class ProgramEntrySerilogExtensionsTests
    {
        [Fact]
        public void SerilogLoggerConfigurationAdded()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);
            var funcMock = new Mock<Action<LoggerConfiguration>>();

            ProgramEntry.CreateBuilder([]).UseSerilog(LogEventLevel.Debug, funcMock.Object);

            funcMock.Verify(funcMock => funcMock(It.IsAny<LoggerConfiguration>()), Times.Once);
        }

        [Fact]
        public async Task VerifySerilogLoggingIsWorkingHappyPath()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            var inMemoryLoggingConfig = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.InMemory();

            await ProgramEntry.CreateBuilder([]).UseSerilog(LogEventLevel.Verbose, (x) =>
            {
                x.WriteTo.InMemory();
            }).Build().RunAsync(_ => Task.FromResult(1));

            Assert.Contains(InMemorySink.Instance.LogEvents, (item) => item.MessageTemplate.ToString().Contains("Starting Program"));
        }

        [Fact]
        public async Task VerifySerilogLoggingIsWorkingExceptionPath()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);

            var inMemoryLoggingConfig = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.InMemory();

            await ProgramEntry.CreateBuilder([]).UseSerilog(LogEventLevel.Verbose, (x) =>
            {
                x.WriteTo.InMemory();
            }).Build().RunAsync(_ => throw new ControlledExitException());

            Assert.Contains(InMemorySink.Instance.LogEvents, (item) => item.Level == LogEventLevel.Debug && item.MessageTemplate.ToString().Contains("Program exited controlled"));
        }
    }
}
