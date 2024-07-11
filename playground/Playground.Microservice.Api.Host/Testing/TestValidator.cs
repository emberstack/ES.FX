using FluentValidation;

namespace Playground.Microservice.Api.Host.Testing;

internal class TestValidator : AbstractValidator<TestRequest>
{
    public TestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}