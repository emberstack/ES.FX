using JetBrains.Annotations;

namespace ES.FX.Problems;

[PublicAPI]
public static class ProblemExtensions
{
    /// <summary>
    ///     Throws a <see cref="ProblemException" /> with the specified <see cref="Problem" />
    /// </summary>
    /// <param name="problem">The <see cref="Problem" /></param>
    /// <exception cref="ProblemException"></exception>
    public static void Throw(this Problem problem) =>
        throw new ProblemException(problem);
}