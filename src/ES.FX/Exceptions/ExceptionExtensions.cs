using JetBrains.Annotations;

namespace ES.FX.Exceptions;

/// <summary>
///     Extension methods for <see cref="Exception" />
/// </summary>
[PublicAPI]
public static class ExceptionExtensions
{
    /// <summary>
    ///     Returns the innermost <see cref="Exception" />
    /// </summary>
    public static Exception InnermostException(this Exception exception)
    {
        while (true)
        {
            if (exception.InnerException == null) return exception;
            exception = exception.InnerException;
        }
    }


    /// <summary>
    ///     Returns the innermost <see cref="Exception" /> of type <see cref="T" />
    /// </summary>
    public static Exception? InnermostException<T>(this Exception? exception) where T : Exception
    {
        if (exception == null) return null;

        T? foundException = null;
        while (exception != null)
        {
            if (exception is T specificException) foundException = specificException;
            exception = exception.InnerException;
        }

        return foundException;
    }
}