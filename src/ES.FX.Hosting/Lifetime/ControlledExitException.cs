namespace ES.FX.Hosting.Lifetime;

/// <summary>
///     Exception used to terminate the application with a specific exit code
/// </summary>
public class ControlledExitException : Exception
{
    public ControlledExitException()
    {
    }

    public ControlledExitException(string message) : base(message)
    {
    }

    public ControlledExitException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public int ExitCode { get; set; } = 0;
}