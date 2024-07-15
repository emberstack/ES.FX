using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Hosting;
using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.NSwag.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.Swashbuckle.Hosting;
using ES.FX.Serilog.Lifetime;
using FluentValidation;
using Playground.Microservice.Api.Host.HostedServices;
using Playground.Microservice.Api.Host.Testing;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    //Serilog
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    builder.Ignite();
    //Fluent Validation
    builder.IgniteFluentValidation();


    //Migrations service
    builder.IgniteMigrationsService();


    //SqlServerDbContext
    builder.IgniteSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext),
        configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                typeof(SimpleDbContextDesignTimeFactory).Assembly.FullName);
        });

    //DbContext Migrations
    builder.AddDbContextMigrationsTask<SimpleDbContext>();


    //Sql Server Client
    builder.IgniteSqlServerClientFactory(nameof(SimpleDbContext));


    // Add health checks UI
    builder.IgniteHealthChecksUi();


    //Add Seq
    builder.IgniteSeqOpenTelemetryExporter();

    builder.Services.AddOpenApiDocument();


    builder.Services.AddHostedService<TestHostedService>();
    builder.Services.AddScoped<IValidator<TestRequest>, TestValidator>();
    builder.Services.AddScoped<IValidator<TestComplexRequest>, TestComplexRequestValidator>();

    var app = builder.Build();
    app.Ignite();

    app.IgniteNSwag();

    app.IgniteHealthChecksUi();


    app.MapGet("/test/exception", void () => throw new Exception("Test exception"))
        .WithName("TestException")
        .WithOpenApi();

    app.MapPost("/test/autoValidation", (TestRequest request) => { return Results.Ok(request); })
        .AddFluentValidationAutoValidation()
        .WithName("TestAutoValidation")
        .WithOpenApi();

    app.MapPost("/test/complex/autoValidation", (TestComplexRequest request) => { return Results.Ok(request); })
        .AddFluentValidationAutoValidation()
        .WithName("TestComplexAutoValidation")
        .WithOpenApi();


    await app.RunAsync();
    return 0;
});