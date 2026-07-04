using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.Hosting.Lifetime;

/// <summary>
///     A wrapper for a program entry point that provides logging, centralized error handling, and exit-action
///     execution (regardless of the exit reason).
/// </summary>
/// <param name="logger">Logger used for log messages</param>
/// <param name="exitActions">List of actions to be executed before the program exits (regardless of the exit reason)</param>
/// <param name="options">Program entry options</param>
public sealed class ProgramEntry(
    ILogger logger,
    IReadOnlyList<Func<ProgramEntryOptions, Task>> exitActions,
    ProgramEntryOptions options)
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgramEntryBuilder" /> class with preconfigured defaults.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The <see cref="ProgramEntryBuilder" />.</returns>
    public static ProgramEntryBuilder CreateBuilder(string[] args) => new(new ProgramEntryOptions { Args = args });

    /// <summary>
    ///     Runs the provided entry point with logging, centralized error handling, and exit-action execution. Unlike the
    ///     <see cref="RunAsync(Func{ProgramEntryOptions, CancellationToken, Task{int}})" /> overload, this overload does not
    ///     register <c>SIGINT</c>/<c>SIGTERM</c>/<c>SIGQUIT</c> handlers, so the entry point is not notified of shutdown
    ///     requests.
    /// </summary>
    /// <param name="action">The entry point to run. Receives the program options.</param>
    /// <returns>The program exit code.</returns>
    public async Task<int> RunAsync(Func<ProgramEntryOptions, Task<int>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            logger.LogTrace("Starting Program");
            var exitCode = await action(options).ConfigureAwait(false);
            logger.LogDebug("Program completed with exit code {exitCode}", exitCode);
            return exitCode;
        }
        catch (ControlledExitException ex)
        {
            logger.LogDebug("Program exited controlled with message \"{message}\" and exit code {exitCode}",
                ex.Message, ex.ExitCode);
            return ex.ExitCode;
        }
        catch (HostAbortedException)
        {
            // Design-time tooling (EF Core tools) and test hosts (WebApplicationFactory) abort the host on
            // purpose and REQUIRE the exception to propagate out of Main to take over the captured host.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Program terminated unexpectedly");
            return 1;
        }
        finally
        {
            await RunExitActionsAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Runs the provided entry point with support for graceful shutdown. A <see cref="CancellationToken" /> is passed to
    ///     the action and is cancelled when the process receives <c>SIGINT</c> (Ctrl+C), <c>SIGTERM</c>, or <c>SIGQUIT</c>,
    ///     allowing long-running entry points to observe the shutdown request and exit cleanly.
    /// </summary>
    /// <param name="action">The entry point to run. Receives the program options and a shutdown cancellation token.</param>
    /// <returns>The program exit code.</returns>
    public async Task<int> RunAsync(Func<ProgramEntryOptions, CancellationToken, Task<int>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var cancellationTokenSource = new CancellationTokenSource();

        void OnSignal(PosixSignalContext context)
        {
            // Prevent the default action (immediate termination) so the entry point can shut down gracefully.
            context.Cancel = true;
            // ReSharper disable once AccessToDisposedClosure
            if (!cancellationTokenSource.IsCancellationRequested) cancellationTokenSource.Cancel();
        }

        using var sigInt = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnSignal);
        using var sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
        using var sigQuit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, OnSignal);

        try
        {
            logger.LogTrace("Starting Program");
            var exitCode = await action(options, cancellationTokenSource.Token).ConfigureAwait(false);
            logger.LogDebug("Program completed with exit code {exitCode}", exitCode);
            return exitCode;
        }
        catch (ControlledExitException ex)
        {
            logger.LogDebug("Program exited controlled with message \"{message}\" and exit code {exitCode}",
                ex.Message, ex.ExitCode);
            return ex.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            logger.LogDebug("Program shut down gracefully in response to a shutdown signal");
            return 0;
        }
        catch (HostAbortedException)
        {
            // Design-time tooling (EF Core tools) and test hosts (WebApplicationFactory) abort the host on
            // purpose and REQUIRE the exception to propagate out of Main to take over the captured host.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Program terminated unexpectedly");
            return 1;
        }
        finally
        {
            await RunExitActionsAsync().ConfigureAwait(false);
        }
    }

    private async Task RunExitActionsAsync()
    {
        foreach (var exitAction in exitActions)
            try
            {
                await exitAction(options).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exit action failed");
            }
    }
}