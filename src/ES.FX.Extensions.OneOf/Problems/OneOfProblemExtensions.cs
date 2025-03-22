using System.Diagnostics.CodeAnalysis;
using ES.FX.Problems;
using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Problems;

public static class OneOfProblemExtensions
{
    [PublicAPI]
    public static bool TryPickProblem(this IOneOfWithProblem oneOf, [NotNullWhen(true)] out Problem? problem)
    {
        if (oneOf.Value is Problem value)
        {
            problem = value;
            return true;
        }

        problem = null;
        return false;
    }
}