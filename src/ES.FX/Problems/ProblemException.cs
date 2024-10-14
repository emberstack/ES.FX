namespace ES.FX.Problems;

/// <summary>
/// Exception that is thrown when a <see cref="Problem"/> occurs. Contains the <see cref="Problem"/> that occured.
/// </summary>
/// <param name="problem">The <see cref="Problem"/></param>
public class ProblemException(Problem problem)
    : Exception($"A problem of type '{problem.Type}' occured. See '{nameof(Problem)}' for more details")
{
    /// <summary>
    /// The source <see cref="Problem"/>
    /// </summary>
    public Problem Problem { get; } = problem;
}