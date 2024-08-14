using ES.FX.Ignite.Swashbuckle.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.IgniteSwashbuckle();

var app = builder.Build();
app.IgniteSwashbuckle();

app.Run();

// ReSharper disable once UnusedMember.Global
public partial class Program
{
}