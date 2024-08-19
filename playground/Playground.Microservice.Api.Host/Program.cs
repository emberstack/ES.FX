using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Azure.Data.Tables.Hosting;
using ES.FX.Ignite.Azure.Storage.Blobs.Hosting;
using ES.FX.Ignite.Azure.Storage.Queues.Hosting;
using ES.FX.Ignite.FluentValidation.Hosting;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Microsoft.Data.SqlClient.Hosting;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.Migrations;
using ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer.Hosting;
using ES.FX.Ignite.Migrations.Hosting;
using ES.FX.Ignite.NSwag.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using ES.FX.Serilog.Lifetime;
using FluentValidation;
using HealthChecks.UI.Client;
using Playground.Microservice.Api.Host.HostedServices;
using Playground.Microservice.Api.Host.Testing;
using Playground.Shared.Data.Simple.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore.SqlServer;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    //Serilog
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    builder.Ignite(settings =>
    {
        settings.HealthChecks.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        settings.OpenTelemetry.AspNetCoreTracingHealthChecksRequestsFiltered = true;
    });


    //Fluent Validation
    builder.IgniteFluentValidation();

    // Add health checks UI
    //builder.IgniteHealthChecksUi();


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


    //Add Seq
    builder.IgniteSeqOpenTelemetryExporter();

    // Add Storage
    builder.IgniteAzureBlobServiceClient();
    builder.IgniteAzureQueueServiceClient();
    builder.IgniteAzureTableServiceClient();

    // Add Redis
    builder.IgniteRedisClient();

    builder.Services.AddOpenApiDocument();


    builder.Services.AddHostedService<TestHostedService>();
    builder.Services.AddScoped<IValidator<TestRequest>, TestValidator>();
    builder.Services.AddScoped<IValidator<TestComplexRequest>, TestComplexRequestValidator>();


    var app = builder.Build();
    app.Ignite();

    app.IgniteNSwag();

    //app.IgniteHealthChecksUi();

    await app.RunAsync();
    return 0;
});