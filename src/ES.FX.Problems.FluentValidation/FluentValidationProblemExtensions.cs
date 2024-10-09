using ES.FX.FluentValidation.Results;
using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.Problems.FluentValidation;

public static class FluentValidationProblemExtensions
{
    [PublicAPI]
    public static ValidationProblem ToValidationProblem(this ValidationResult validationResult) =>
        new(validationResult.ToValidationErrors());
}