using ES.FX.Hosting.Lifetime;
using ES.FX.Shared.Tests.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests
{
    public class ProgramEntryBuilderTests
    {
        [Fact]
        public async Task VerifyCustomLoggerCanBeAdded()
        {
            var mock = new Mock<ILogger<ProgramEntryBuilder>>();

            ProgramEntryBuilder programEntryBuilder = new ProgramEntryBuilder(new ProgramEntryOptions());
            programEntryBuilder.WithLogger(mock.Object);
            await programEntryBuilder.Build().RunAsync(_ => Task.FromResult(0));

            LoggerTestHelper.VerifyLoggerWasCalled(mock, string.Empty, LogLevel.Trace);
        }

        [Fact]
        public async Task VerifyExitActionsCanBeAdded()
        {
            var funcMock = new Mock<Func<ProgramEntryOptions, Task>>();

            ProgramEntryBuilder programEntryBuilder = new ProgramEntryBuilder(new ProgramEntryOptions());
            programEntryBuilder.AddExitAction(funcMock.Object);
            await programEntryBuilder.Build().RunAsync(_ => Task.FromResult(0));

            funcMock.Verify(funcMock => funcMock(It.IsAny<ProgramEntryOptions>()), Times.Once);
        }
    }
}