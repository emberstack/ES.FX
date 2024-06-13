using Microsoft.Extensions.Logging;

namespace ES.FX.Hosting.Lifetime;

/// <summary>
///     A wrapper for program entry point with support for logging and graceful shutdown.
/// </summary>
/// <param name="logger">Logger used for log messages</param>
/// <param name="exitActions">List of actions to be executed before the program exits (regardless of the exit reason)</param>
/// <param name="options">Program entry options</param>
public sealed class ProgramEntry(
    ILogger logger,
    IReadOnlyList<Func<ProgramEntryOptions, Task>> exitActions,
    ProgramEntryOptions options)
{
    public async Task<int> RunAsync(Func<ProgramEntryOptions, Task<int>> action)
    {
        try
        {
            logger.LogTrace("Starting Program");
            var exitCode = await action(options);
            logger.LogDebug("Program completed with exit code {exitCode}", exitCode);
            return exitCode;
        }
        catch (ControlledExitException ex)
        {
            logger.LogDebug("Program exited controlled with message \"{message}\" and exit code {exitCode}",
                ex.Message, ex.ExitCode);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Program terminated unexpectedly");
            return 1;
        }
        finally
        {
            foreach (var exitAction in exitActions) await exitAction(options);
        }
    }


    /// <summary>
    ///     Initializes a new instance of the <see cref="ProgramEntryBuilder" /> class with preconfigured defaults.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The <see cref="ProgramEntryBuilder" />.</returns>
    public static ProgramEntryBuilder CreateBuilder(string[] args) => new(new ProgramEntryOptions { Args = args });
}