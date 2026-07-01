using System.Diagnostics.CodeAnalysis;
using ES.FX.Problems;
using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Problems;

/// <summary>
///     Extension methods for unions that implement <see cref="IOneOfWithProblem" />.
/// </summary>
public static class OneOfProblemExtensions
{
    /// <summary>
    ///     Attempts to extract a <see cref="Problem" /> from the union's current value, regardless of which case slot
    ///     it occupies.
    /// </summary>
    /// <param name="oneOf">The union to inspect.</param>
    /// <param name="problem">
    ///     When this method returns <see langword="true" />, contains the <see cref="Problem" /> held by the union;
    ///     otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> if the union's current value is a <see cref="Problem" />; otherwise
    ///     <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="oneOf" /> is <see langword="null" />.</exception>
    [PublicAPI]
    public static bool TryPickProblem(this IOneOfWithProblem oneOf, [NotNullWhen(true)] out Problem? problem)
    {
        ArgumentNullException.ThrowIfNull(oneOf);

        if (oneOf.Value is Problem value)
        {
            problem = value;
            return true;
        }

        problem = null;
        return false;
    }
}