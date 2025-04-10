using ES.FX.Problems;
using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.Extensions.FluentValidation.Results;

public static class ValidationResultExtensions
{
    /// <summary>
    ///     Converts the <see cref="ValidationResult" /> to a dictionary of validation errors
    /// </summary>
    [PublicAPI]
    public static IDictionary<string, List<ErrorDetail>> ToValidationErrors(this ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(validationFailure => validationFailure.PropertyName)
            .ToDictionary(
                validationFailureGrouping => validationFailureGrouping.Key,
                validationFailureGrouping => validationFailureGrouping
                    .Select(validationFailure => new ErrorDetail
                    {
                        ErrorMessage = validationFailure.ErrorMessage,
                        ErrorCode = validationFailure.ErrorCode
                    })
                    .ToList());
    }
}