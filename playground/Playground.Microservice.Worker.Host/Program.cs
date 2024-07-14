using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Playground.Microservice.Worker.Host.HostedServices;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

return await ProgramEntry.CreateBuilder(args).Build().RunAsync(async options =>
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Ignite();

    //Serilog
    builder.IgniteSerilog();

    // Migrations service
    builder.IgniteMigrationsService();


    //SqlServerDbContext
    builder.IgniteSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext),
        configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(typeof(SimpleDbContextDesignTimeFactory).Assembly
                .FullName);
        });

    //DbContext Migrations
    builder.AddDbContextMigrationsTask<SimpleDbContext>();


    //Sql Server Client
    builder.IgniteSqlServerClientFactory(nameof(SimpleDbContext));

    builder.Services.AddHostedService<TestHostedService>();

    var app = builder.Build();
    app.Ignite();


    await app.RunAsync();
    return 0;
});