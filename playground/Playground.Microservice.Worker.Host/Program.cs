using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using Microsoft.Extensions.Hosting;

return await ProgramEntry.CreateBuilder(args).Build().RunAsync(async options =>
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.AddIgnite();

    var app = builder.Build();
    app.UseIgnite();

    await app.RunAsync();
    return 0;
});