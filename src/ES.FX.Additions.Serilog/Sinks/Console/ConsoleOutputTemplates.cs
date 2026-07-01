using JetBrains.Annotations;

namespace ES.FX.Additions.Serilog.Sinks.Console;

/// <summary>
///     Output templates for the Serilog console sink
/// </summary>
[PublicAPI]
public static class ConsoleOutputTemplates
{
    /// <summary>
    ///     The default console output template
    /// </summary>
    public const string Default =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
}