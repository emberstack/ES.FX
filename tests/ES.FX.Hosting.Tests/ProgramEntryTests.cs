using ES.FX.Hosting.Lifetime;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

public class ProgramEntryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Handle_CleanExit(int exitCode)
    {
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());

        var programEntry = builder.Build();

        var result = await programEntry.RunAsync(_ => Task.FromResult(exitCode));

        Assert.Equal(exitCode, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Handle_ControlledExit(int exitCode)
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);

        var programEntry = builder.Build();

        var result = await programEntry.RunAsync(_ => throw new ControlledExitException
        {
            ExitCode = exitCode
        });

        Assert.Equal(exitCode, result);

        loggerMock.VerifyLoggerWasCalled("Program exited controlled");
    }

    [Fact]
    public async Task Handle_UnexpectedError()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);

        var programEntry = builder.Build();

        var result = await programEntry.RunAsync(_ => throw new Exception());

        Assert.Equal(1, result);
        loggerMock.VerifyLoggerWasCalled("Program terminated unexpectedly", LogLevel.Critical);
    }
}