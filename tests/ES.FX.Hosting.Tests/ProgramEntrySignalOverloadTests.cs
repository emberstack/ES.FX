using System.Runtime.InteropServices;
using ES.FX.Hosting.Lifetime;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

/// <summary>
///     Tests for the signal-aware
///     <see cref="ProgramEntry.RunAsync(Func{ProgramEntryOptions, CancellationToken, Task{int}})" /> overload: the
///     graceful-shutdown feature, its cancellation semantics, and its error-handling branches.
/// </summary>
public class ProgramEntrySignalOverloadTests
{
    private static ProgramEntry BuildWith(Mock<ILogger<ProgramEntry>> loggerMock,
        params Func<ProgramEntryOptions, Task>[] exitActions)
    {
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        builder.WithLogger(loggerMock.Object);
        foreach (var exitAction in exitActions) builder.AddExitAction(exitAction);
        return builder.Build();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public async Task CleanExit_ReturnsActionExitCode(int exitCode)
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        var result = await programEntry.RunAsync((_, _) => Task.FromResult(exitCode));

        Assert.Equal(exitCode, result);
        loggerMock.VerifyLoggerWasCalled("Program completed with exit code");
    }

    [Fact]
    public async Task CleanExit_PassesCancellableNonCancelledTokenToAction()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        var observedCancelled = true;
        var tokenCanBeCancelled = false;

        var result = await programEntry.RunAsync((_, ct) =>
        {
            observedCancelled = ct.IsCancellationRequested;
            tokenCanBeCancelled = ct.CanBeCanceled;
            return Task.FromResult(0);
        });

        Assert.Equal(0, result);
        Assert.False(observedCancelled);
        // The token must be a real, cancellable token backed by the internal CTS (not CancellationToken.None).
        Assert.True(tokenCanBeCancelled);
    }

    [Fact]
    public async Task ControlledExit_ReturnsExitCode_AndLogsDebug()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        var result = await programEntry.RunAsync((_, _) => throw new ControlledExitException("bye")
        {
            ExitCode = 5
        });

        Assert.Equal(5, result);
        loggerMock.VerifyLoggerWasCalled("Program exited controlled");
    }

    [Fact]
    public async Task GenericException_Returns1_AndLogsCritical()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        var result = await programEntry.RunAsync((_, _) => throw new InvalidOperationException("boom"));

        Assert.Equal(1, result);
        loggerMock.VerifyLoggerWasCalled("Program terminated unexpectedly", LogLevel.Critical);
    }

    [Fact]
    public async Task OperationCanceled_WhenTokenNotCancelled_FallsThroughToGenericCatch_Returns1()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        // Action throws OperationCanceledException but the framework's CTS was never cancelled (no signal),
        // so the 'when (IsCancellationRequested)' filter is false and it must fall through to the generic
        // catch -> return 1 + LogCritical. This confirms the 'when' filter guards the graceful branch.
        var result = await programEntry.RunAsync((_, _) =>
            throw new OperationCanceledException("not a shutdown"));

        Assert.Equal(1, result);
        loggerMock.VerifyLoggerWasCalled("Program terminated unexpectedly", LogLevel.Critical);
    }

    [Fact]
    public async Task NullAction_ThrowsArgumentNullException()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            programEntry.RunAsync((Func<ProgramEntryOptions, CancellationToken, Task<int>>)null!));
    }

    [Fact]
    public async Task ExitActions_RunOnCleanExit()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();
        exitActionMock.Setup(f => f(It.IsAny<ProgramEntryOptions>())).Returns(Task.CompletedTask);
        var programEntry = BuildWith(loggerMock, exitActionMock.Object);

        await programEntry.RunAsync((_, _) => Task.FromResult(0));

        exitActionMock.Verify(f => f(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }

    [Fact]
    public async Task ExitActions_RunOnGenericException()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();
        exitActionMock.Setup(f => f(It.IsAny<ProgramEntryOptions>())).Returns(Task.CompletedTask);
        var programEntry = BuildWith(loggerMock, exitActionMock.Object);

        var result = await programEntry.RunAsync((_, _) => throw new Exception("boom"));

        Assert.Equal(1, result);
        exitActionMock.Verify(f => f(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }

    [Fact]
    public async Task ExitActions_RunOnControlledExit()
    {
        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var exitActionMock = new Mock<Func<ProgramEntryOptions, Task>>();
        exitActionMock.Setup(f => f(It.IsAny<ProgramEntryOptions>())).Returns(Task.CompletedTask);
        var programEntry = BuildWith(loggerMock, exitActionMock.Object);

        var result = await programEntry.RunAsync((_, _) => throw new ControlledExitException { ExitCode = 3 });

        Assert.Equal(3, result);
        exitActionMock.Verify(f => f(It.IsAny<ProgramEntryOptions>()), Times.Once);
    }

    /// <summary>
    ///     Drives the full graceful-shutdown flow through a REAL POSIX signal (SIGINT). The signal handler the
    ///     entry point registers must cancel the token, and the action observing that cancellation and throwing an
    ///     <see cref="OperationCanceledException" /> must map to exit code 0 with a Debug 'shut down gracefully' log.
    ///     Skipped on Windows where raising SIGINT into the current process is not reliably supported by
    ///     <see cref="PosixSignalRegistration" /> from managed code.
    /// </summary>
    [Fact]
    public async Task GracefulShutdown_RealSignal_CancelsToken_Returns0_AndLogsDebug()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // SIGINT raise is not deterministic on Windows; covered on Linux CI.

        var loggerMock = new Mock<ILogger<ProgramEntry>>();
        var programEntry = BuildWith(loggerMock);

        var result = await programEntry.RunAsync(async (_, ct) =>
        {
            // Raise SIGINT so the entry point's registered handler cancels the CTS backing 'ct'.
            RaiseSignal(2); // SIGINT == 2

            // Wait for the handler to observe and propagate cancellation, then throw an OCE tied to the token.
            var start = DateTime.UtcNow;
            while (!ct.IsCancellationRequested && DateTime.UtcNow - start < TimeSpan.FromSeconds(5))
                await Task.Delay(10, CancellationToken.None);

            ct.ThrowIfCancellationRequested();
            return 99; // should never be reached
        });

        Assert.Equal(0, result);
        loggerMock.VerifyLoggerWasCalled("shut down gracefully");
    }

    [DllImport("libc", EntryPoint = "raise", SetLastError = true)]
    private static extern int RaiseSignal(int signal);
}