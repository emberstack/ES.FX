using Microsoft.Extensions.Logging;

namespace ES.FX.Hosting.Lifetime;


/// <summary>
/// Builder for creating a <see cref="ProgramEntry"/> instance
/// </summary>
public class ProgramEntryBuilder
{
    private ILogger _logger;
    private readonly List<Func<ProgramEntryOptions, Task>> _exitActions = [];
    private readonly ProgramEntryOptions _options;

    public ProgramEntryBuilder(ProgramEntryOptions options)
    {
        _options = options;
        _logger = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        }).CreateLogger<ProgramEntry>();
    }

    /// <summary>
    /// Sets the logger to be used by the <see cref="ProgramEntry"/>
    /// </summary>
    /// <param name="logger">The new <see cref="ILogger"/> instance</param>
    /// <returns>The <see cref="ProgramEntryBuilder"/></returns>
    public ProgramEntryBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Adds a new action to be executed before the program exits (regardless of the exit reason)
    /// </summary>
    /// <param name="exitAction">Func to execute on exit</param>
    /// <returns>The <see cref="ProgramEntryBuilder"/></returns>
    public ProgramEntryBuilder AddExitAction(Func<ProgramEntryOptions, Task> exitAction)
    {
        _exitActions.Add(exitAction);
        return this;
    }

    /// <summary>
    /// Builds the <see cref="ProgramEntry"/> instance
    /// </summary>
    /// <returns>The <see cref="ProgramEntry"/> instance</returns>
    public ProgramEntry Build()
    {
        return new ProgramEntry(_logger, _exitActions, new ProgramEntryOptions { Args = _options.Args });
    }
}