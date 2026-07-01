using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.Additions.FluentValidation.Results;

public static class ValidationResultExtensions
{
    /// <summary>
    ///     Converts the <see cref="ValidationResult" /> to a dictionary of validation errors
    /// </summary>
    [PublicAPI]
    public static IDictionary<string, string[]> ToValidationErrors(this ValidationResult validationResult) =>
        validationResult.ToDictionary();
}