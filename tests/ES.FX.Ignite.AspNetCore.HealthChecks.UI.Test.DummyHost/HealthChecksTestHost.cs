using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using Microsoft.AspNetCore.Builder;

internal sealed class HealthChecksTestHost
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.IgniteHealthChecksUi();

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.IgniteHealthChecksUi();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
