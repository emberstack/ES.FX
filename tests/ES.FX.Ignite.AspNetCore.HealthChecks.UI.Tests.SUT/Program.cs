using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.IgniteHealthChecksUi();

var app = builder.Build();
app.IgniteHealthChecksUi();

app.Run();

// ReSharper disable once UnusedMember.Global
namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests.SUT
{
    public class Program;
}