using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Serilog.Lifetime;
using Playground.Microservice.Api.Host.HostedServices;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddIgnite();

    builder.AddSerilog();
    builder.AddMigrationsService();

    builder.AddSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext),
        configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(typeof(DummyDbContextDesignTimeFactory).Assembly
                .FullName);
        });
    builder.AddDbContextMigrationsTask<SimpleDbContext>();


    builder.Services.AddHostedService<TestHostedService>();

    var app = builder.Build();
    app.UseIgnite();


    await app.RunAsync();
    return 0;
});