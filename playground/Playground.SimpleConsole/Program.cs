using ES.FX.Extensions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(options =>
{
    throw new Exception("Test exception");

    //return Task.FromResult(1);
});