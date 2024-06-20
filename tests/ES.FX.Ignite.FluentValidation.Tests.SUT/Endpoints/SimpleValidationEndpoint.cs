using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static Microsoft.AspNetCore.Http.TypedResults;

namespace ES.FX.Ignite.FluentValidation.Tests.SUT.Endpoints;

public class SimpleValidationEndpoint
{
    public const string RoutePattern = "/simpleValidation";

    public static void AddServices(IServiceCollection services)
    {
        services.AddTransient<IValidator<Request>, TestValidator>();
    }


    public static async Task<Results<Ok<Response>, ValidationProblem, BadRequest<ProblemDetails>>> Handle(
        ILogger<SimpleValidationEndpoint> logger,
        [FromBody] Request request,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return Ok(new Response(request.Name));
    }

    public static void MapRoutes(IEndpointRouteBuilder builder)
    {
        builder.MapPost(RoutePattern, Handle);
    }

    public record Request(string Name);

    public record Response(string Name);

    public class TestValidator : AbstractValidator<Request>
    {
        public TestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}