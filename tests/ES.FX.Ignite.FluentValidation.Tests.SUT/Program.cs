using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.FluentValidation.Tests.SUT.Endpoints;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.IgniteFluentValidation();

SimpleValidationEndpoint.AddServices(builder.Services);


var app = builder.Build();
var root = app
    .MapGroup(string.Empty)
    .AddFluentValidationAutoValidation();

SimpleValidationEndpoint.MapRoutes(root);

app.Run();

// ReSharper disable once UnusedMember.Global
namespace ES.FX.Ignite.FluentValidation.Tests.SUT
{
    public class Program;
}