using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

internal sealed class FluentValidationTestHost
{
    internal static string APIRoute = "/test/autoValidation";
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddScoped<IValidator<TestRequest>, TestValidator>();

        var app = builder.Build();

        app.MapPost(APIRoute, (TestRequest request) => { return Results.Ok(request); })
        .AddFluentValidationAutoValidation()
        .WithName("TestAutoValidation");

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}

public record TestRequest(string Name);


internal class TestValidator : AbstractValidator<TestRequest>
{
    public TestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}