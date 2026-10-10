using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ES.FX.Hosting.Lifetime;
using ES.FX.Hosting.Tests.SUT;
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
    ///     Drives the full graceful-shutdown flow through a REAL POSIX signal (SIGINT). The signal goes to a child
    ///     process (<c>ES.FX.Hosting.Tests.SUT</c>) running the entry point, never to the test process itself: the
    ///     Microsoft.Testing.Platform host treats SIGINT as Ctrl+C and aborts the whole test session. The handler the
    ///     entry point registers must cancel the token, and the resulting <see cref="OperationCanceledException" />
    ///     must map to exit code 0 with a Debug 'shut down gracefully' log. Not discovered on Windows, which has no
    ///     POSIX signals to send to another process (see <see cref="PosixFactAttribute" />).
    /// </summary>
    [PosixFact]
    public async Task GracefulShutdown_RealSignal_CancelsToken_Returns0_AndLogsDebug()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var output = new ConcurrentQueue<string>();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var sut = new Process();
        sut.StartInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(SutMarkers).Assembly.Location])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        sut.OutputDataReceived += OnOutput;
        sut.ErrorDataReceived += OnOutput;
        sut.Start();
        sut.BeginOutputReadLine();
        sut.BeginErrorReadLine();

        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            Assert.Equal(0, SendSignal(sut.Id, 2)); // SIGINT == 2
            await sut.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The SUT did not finish in time. Output:{Environment.NewLine}" +
                        string.Join(Environment.NewLine, output));
        }
        finally
        {
            if (!sut.HasExited) sut.Kill(true);
        }

        Assert.Equal(0, sut.ExitCode);
        Assert.Contains(output, line => line.Contains("shut down gracefully"));
        return;

        void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            output.Enqueue(e.Data);
            if (e.Data == SutMarkers.Ready) ready.TrySetResult();
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int pid, int signal);
}