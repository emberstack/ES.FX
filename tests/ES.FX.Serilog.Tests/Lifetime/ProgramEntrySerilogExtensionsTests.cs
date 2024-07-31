using ES.FX.Hosting.Lifetime;
using ES.FX.Serilog.Lifetime;
using Microsoft.Extensions.Hosting;
using Moq;
using Serilog;
using Serilog.Events;

namespace ES.FX.Serilog.Tests.Lifetime
{
    public class ProgramEntrySerilogExtensionsTests
    {
        [Fact]
        public void Serilog_LoggerConfigurationAdded()
        {
            var builder = Host.CreateEmptyApplicationBuilder(null);
            var funcMock = new Mock<Action<LoggerConfiguration>>();

            ProgramEntry.CreateBuilder([]).UseSerilog(LogEventLevel.Debug, funcMock.Object);

            funcMock.Verify(funcMock => funcMock(It.IsAny<LoggerConfiguration>()), Times.Once);
        }
    }
}
