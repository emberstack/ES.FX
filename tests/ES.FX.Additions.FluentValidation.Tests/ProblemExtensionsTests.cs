using ES.FX.Additions.FluentValidation.Problems;
using ES.FX.Additions.FluentValidation.Results;
using ES.FX.Problems;
using FluentValidation.Results;

namespace ES.FX.Additions.FluentValidation.Tests;

public class ProblemExtensionsTests
{
    [Fact]
    public void ToValidationProblem_ValidResult_ProducesProblemWithNoErrors()
    {
        var problem = new ValidationResult().ToValidationProblem();

        Assert.NotNull(problem);
        Assert.IsType<ValidationProblem>(problem);
        Assert.Empty(problem.Errors);
    }

    [Fact]
    public void ToValidationProblem_CarriesFailuresAsErrors()
    {
        var result = new ValidationResult([
            new ValidationFailure("Email", "Email is invalid"),
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Password", "Password is too weak")
        ]);

        var problem = result.ToValidationProblem();

        Assert.Equal(2, problem.Errors.Count);
        Assert.Equal(2, problem.Errors["Email"].Length);
        Assert.Contains("Email is invalid", problem.Errors["Email"]);
        Assert.Contains("Email is required", problem.Errors["Email"]);
        Assert.Equal(["Password is too weak"], problem.Errors["Password"]);
    }

    [Fact]
    public void ToValidationProblem_ErrorsMatch_ToValidationErrors()
    {
        var result = new ValidationResult([
            new ValidationFailure("Field", "Bad")
        ]);

        var problem = result.ToValidationProblem();
        var errors = result
            .ToValidationErrors();

        Assert.Equal(errors, problem.Errors);
    }

    [Fact]
    public void ToValidationProblem_SetsStandardProblemMetadata()
    {
        var problem = new ValidationResult().ToValidationProblem();

        // Metadata inherited from the ValidationProblem constructor: the second
        // positional base(...) argument maps to Title, not Detail.
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Null(problem.Detail);
    }
}