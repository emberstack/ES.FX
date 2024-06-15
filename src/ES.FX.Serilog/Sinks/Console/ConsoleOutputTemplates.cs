namespace ES.FX.Serilog.Sinks.Console;

public static class ConsoleOutputTemplates
{
    public const string Default =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
}