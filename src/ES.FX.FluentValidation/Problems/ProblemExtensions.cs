using ES.FX.FluentValidation.Results;
using ES.FX.Problems;
using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.FluentValidation.Problems;

public static class ProblemExtensions
{
    [PublicAPI]
    public static ValidationProblem ToValidationProblem(this ValidationResult validationResult) =>
        new(validationResult.ToValidationErrors());
}