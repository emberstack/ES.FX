using ES.FX.Hosting.Lifetime;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

/// <summary>
///     Coverage for <c>RunExitActionsAsync</c> error-isolation semantics: a throwing exit action must be caught,
///     logged as "Exit action failed" at <see cref="LogLevel.Error" />, must not prevent subsequent exit actions
///     from running, and must not alter the returned exit code.
/// </summary>
public class ProgramEntryExitActionTests
{
    [Fact]
    public async Task ThrowingExitAction_IsIsolated_SubsequentActionsRun_ExitCodeUnchanged()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();

        var first = false;
        var third = false;

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);
        builder.AddExitAction(_ =>
        {
            first = true;
            return Task.CompletedTask;
        });
        builder.AddExitAction(_ => throw new InvalidOperationException("exit action boom"));
        builder.AddExitAction(_ =>
        {
            third = true;
            return Task.CompletedTask;
        });

        var programEntry = builder.Build();

        // The action itself exits cleanly with code 4; a throwing exit action must NOT change that.
        var result = await programEntry.RunAsync(_ => Task.FromResult(4));

        Assert.Equal(4, result);
        Assert.True(first, "The exit action before the throwing one should have run.");
        Assert.True(third, "The exit action after the throwing one must still run (swallow-and-continue).");
        loggerMock.VerifyLoggerWasCalled("Exit action failed", LogLevel.Error);
    }

    [Fact]
    public async Task ThrowingExitAction_OnSignalOverload_IsIsolated_ExitCodeUnchanged()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();

        var second = false;

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);
        builder.AddExitAction(_ => throw new InvalidOperationException("exit action boom"));
        builder.AddExitAction(_ =>
        {
            second = true;
            return Task.CompletedTask;
        });

        var programEntry = builder.Build();

        var result = await programEntry.RunAsync((_, _) => Task.FromResult(9));

        Assert.Equal(9, result);
        Assert.True(second, "A later exit action must still run after an earlier one throws.");
        loggerMock.VerifyLoggerWasCalled("Exit action failed", LogLevel.Error);
    }

    [Fact]
    public async Task ExitActions_RunOnGenericException_FirstOverload()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();
        exitActionMock.Setup(f => f(It.IsAny<ProgramEntryOptions>())).Returns(Task.CompletedTask);

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);
        builder.AddExitAction(exitActionMock.Object);

        var result = await builder.Build().RunAsync(_ => throw new Exception("boom"));

        Assert.Equal(1, result);
        exitActionMock.Verify(f => f(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }

    [Fact]
    public async Task ExitActions_RunOnControlledExit_FirstOverload()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();
        exitActionMock.Setup(f => f(It.IsAny<ProgramEntryOptions>())).Returns(Task.CompletedTask);

        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);
        builder.AddExitAction(exitActionMock.Object);

        var result = await builder.Build().RunAsync(_ => throw new ControlledExitException { ExitCode = 2 });

        Assert.Equal(2, result);
        exitActionMock.Verify(f => f(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }
}