using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.FluentValidation.Results;

public static class ValidationResultExtensions
{
    [PublicAPI]
    public static IDictionary<string, string[]> ToValidationErrors(this ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(validationFailure => validationFailure.PropertyName)
            .ToDictionary(
                validationFailureGrouping => validationFailureGrouping.Key,
                validationFailureGrouping => validationFailureGrouping
                    .Select(validationFailure => validationFailure.ErrorMessage).ToArray());
    }
}