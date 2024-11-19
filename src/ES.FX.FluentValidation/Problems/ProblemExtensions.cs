﻿using ES.FX.FluentValidation.Results;
using ES.FX.Problems;
using FluentValidation.Results;
using JetBrains.Annotations;

namespace ES.FX.FluentValidation.Problems;

public static class ProblemExtensions
{
    /// <summary>
    ///     Creates a <see cref="ValidationProblem" /> from the <see cref="ValidationResult" />
    /// </summary>
    [PublicAPI]
    public static ValidationProblem ToValidationProblem(this ValidationResult validationResult) =>
        new(validationResult.ToValidationErrors());
}