using FluentValidation;

namespace Playground.Microservice.Api.Host.Testing;

internal class TestComplexRequestValidator : AbstractValidator<TestComplexRequest>
{
    public TestComplexRequestValidator(IValidator<TestRequest> itemValidator)
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(itemValidator);
    }
}