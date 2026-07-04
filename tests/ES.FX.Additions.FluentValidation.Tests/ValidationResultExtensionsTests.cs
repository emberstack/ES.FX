using ES.FX.Additions.FluentValidation.Results;
using FluentValidation;
using FluentValidation.Results;

namespace ES.FX.Additions.FluentValidation.Tests;

public class ValidationResultExtensionsTests
{
    [Fact]
    public void ToValidationErrors_ValidResult_ReturnsEmptyDictionary()
    {
        var result = new ValidationResult();

        var errors = result.ToValidationErrors();

        Assert.NotNull(errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void ToValidationErrors_ValidatorPassesForValidObject_ReturnsEmpty()
    {
        var result = new PersonValidator().Validate(new Person("Alice", 30));

        Assert.True(result.IsValid);
        Assert.Empty(result.ToValidationErrors());
    }

    [Fact]
    public void ToValidationErrors_MapsPropertyNameToItsMessages()
    {
        var result = new ValidationResult([
            new ValidationFailure("Name", "Name is required")
        ]);

        var errors = result.ToValidationErrors();

        Assert.True(errors.ContainsKey("Name"));
        Assert.Equal(["Name is required"], errors["Name"]);
    }

    [Fact]
    public void ToValidationErrors_GroupsMultipleMessagesForSameProperty()
    {
        var result = new ValidationResult([
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Name", "Name is too short")
        ]);

        var errors = result.ToValidationErrors();

        Assert.Single(errors);
        Assert.Equal(2, errors["Name"].Length);
        Assert.Contains("Name is required", errors["Name"]);
        Assert.Contains("Name is too short", errors["Name"]);
    }

    [Fact]
    public void ToValidationErrors_SeparatesDistinctProperties()
    {
        var result = new ValidationResult([
            new ValidationFailure("Name", "Name is required"),
            new ValidationFailure("Age", "Age must be non-negative")
        ]);

        var errors = result.ToValidationErrors();

        Assert.Equal(2, errors.Count);
        Assert.Equal(["Name is required"], errors["Name"]);
        Assert.Equal(["Age must be non-negative"], errors["Age"]);
    }

    [Fact]
    public void ToValidationErrors_FromRealValidatorFailure_ContainsFailedProperties()
    {
        var result = new PersonValidator().Validate(new Person(string.Empty, -5));

        var errors = result.ToValidationErrors();

        Assert.False(result.IsValid);
        Assert.True(errors.ContainsKey(nameof(Person.Name)));
        Assert.True(errors.ContainsKey(nameof(Person.Age)));
        Assert.NotEmpty(errors[nameof(Person.Name)]);
        Assert.NotEmpty(errors[nameof(Person.Age)]);
    }

    [Fact]
    public void ToValidationErrors_ReturnValue_IsExpectedInterfaceType()
    {
        var result = new ValidationResult();

        var errors = result.ToValidationErrors();

        Assert.NotNull(errors);
    }

    private sealed record Person(string Name, int Age);

    private sealed class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(p => p.Name).NotEmpty();
            RuleFor(p => p.Age).GreaterThanOrEqualTo(0);
        }
    }
}