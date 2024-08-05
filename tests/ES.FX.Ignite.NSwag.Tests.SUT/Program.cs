using ES.FX.Ignite.NSwag.Hosting;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();
app.IgniteNSwag();

app.Run();

// ReSharper disable once UnusedMember.Global
public partial class Program
{
}