using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.Seq.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Serilog.Lifetime;
using Playground.Microservice.Api.Host.HostedServices;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    //Serilog
    builder.Logging.ClearProviders();
    builder.AddIgniteSerilog();

    builder.AddIgnite();


    //Migrations service
    builder.AddIgniteMigrationsService();


    //SqlServerDbContext
    builder.AddIgniteSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext),
        configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                typeof(SimpleDbContextDesignTimeFactory).Assembly.FullName);
        });

    //DbContext Migrations
    builder.AddDbContextMigrationsTask<SimpleDbContext>();


    //Sql Server Client
    builder.AddIgniteSqlServerClientFactory(nameof(SimpleDbContext));


    // Add health checks UI
    builder.AddIgniteHealthChecksUi();


    //Add Seq
    builder.AddIgniteSeq("dev");


    builder.Services.AddHostedService<TestHostedService>();

    var app = builder.Build();
    app.UseIgnite();

    app.UseIgniteHealthChecksUi();


    await app.RunAsync();
    return 0;
});