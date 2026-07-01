namespace ES.FX.Hosting.Lifetime;

/// <summary>
///     Exception used to terminate the application with a specific exit code
/// </summary>
public class ControlledExitException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ControlledExitException" /> class
    /// </summary>
    public ControlledExitException()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ControlledExitException" /> class with a specified message
    /// </summary>
    /// <param name="message">The message that describes the reason for the exit</param>
    public ControlledExitException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ControlledExitException" /> class with a specified message and a
    ///     reference to the inner exception that caused the exit
    /// </summary>
    /// <param name="message">The message that describes the reason for the exit</param>
    /// <param name="innerException">The exception that caused the exit</param>
    public ControlledExitException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    ///     The exit code the program should exit with. Defaults to <c>0</c>
    /// </summary>
    public int ExitCode { get; set; } = 0;
}