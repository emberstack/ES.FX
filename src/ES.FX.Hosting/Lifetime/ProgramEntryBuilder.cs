using Microsoft.Extensions.Logging;

namespace ES.FX.Hosting.Lifetime;

/// <summary>
///     Builder for creating a <see cref="ProgramEntry" /> instance
/// </summary>
public class ProgramEntryBuilder(ProgramEntryOptions options)
{
    private readonly List<Func<ProgramEntryOptions, Task>> _exitActions = [];
    private ILogger _logger = LoggerFactory.Create(builder => { builder.AddConsole(); }).CreateLogger<ProgramEntry>();

    /// <summary>
    ///     Adds a new action to be executed before the program exits (regardless of the exit reason)
    /// </summary>
    /// <param name="exitAction">Func to execute on exit</param>
    /// <returns>The <see cref="ProgramEntryBuilder" /></returns>
    public ProgramEntryBuilder AddExitAction(Func<ProgramEntryOptions, Task> exitAction)
    {
        _exitActions.Add(exitAction);
        return this;
    }

    /// <summary>
    ///     Builds the <see cref="ProgramEntry" /> instance
    /// </summary>
    /// <returns>The <see cref="ProgramEntry" /> instance</returns>
    public ProgramEntry Build() => new(_logger, _exitActions, new ProgramEntryOptions { Args = options.Args });

    /// <summary>
    ///     Sets the logger to be used by the <see cref="ProgramEntry" />
    /// </summary>
    /// <param name="logger">The new <see cref="ILogger" /> instance</param>
    /// <returns>The <see cref="ProgramEntryBuilder" /></returns>
    public ProgramEntryBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }
}