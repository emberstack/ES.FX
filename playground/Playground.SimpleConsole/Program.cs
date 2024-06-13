using ES.FX.Hosting.Lifetime;
using ES.FX.Serilog.Lifetime;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(options =>
{
    throw new Exception("Test exception");

    //return Task.FromResult(1);
});