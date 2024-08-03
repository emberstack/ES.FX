using ES.FX.Hosting.Lifetime;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

public class ProgramEntryBuilderTests
{
    [Fact]
    public async Task CustomLogger_Added()
    {
        var mock = new Mock<ILogger<ProgramEntryBuilder>>();

        var programEntryBuilder = new ProgramEntryBuilder(new ProgramEntryOptions());
        programEntryBuilder.WithLogger(mock.Object);
        await programEntryBuilder.Build().RunAsync(_ => Task.FromResult(0));

        mock.VerifyLoggerWasCalled(string.Empty, LogLevel.Trace);
    }

    [Fact]
    public async Task ExitActions_Added()
    {
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();

        var programEntryBuilder = new ProgramEntryBuilder(new ProgramEntryOptions());
        programEntryBuilder.AddExitAction(exitActionMock.Object);
        await programEntryBuilder.Build().RunAsync(_ => Task.FromResult(0));

        exitActionMock.Verify(expression =>
            expression(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }
}