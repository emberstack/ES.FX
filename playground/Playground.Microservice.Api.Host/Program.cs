using Asp.Versioning;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Asp.Versioning.Hosting;
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
using ES.FX.NSwag.AspNetCore.Generation;
using ES.FX.Serilog.Lifetime;
using ES.FX.TransactionalOutbox.EntityFrameworkCore;
using ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    builder.Ignite(settings =>
    {
        settings.HealthChecks.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        //settings.OpenTelemetry.AspNetCoreTracingHealthChecksRequestsFiltered = true;
    });

    //Add Seq
    builder.IgniteSeqOpenTelemetryExporter();

    //Fluent Validation
    builder.IgniteFluentValidation();

    //Migrations service
    builder.IgniteMigrationsService();

    #region Ignite API Versioning

    var apiVersions = new[]
    {
        new ApiVersion(1)
    }.Order().ToArray();
    builder.Services.AddSingleton<IEnumerable<ApiVersion>>(apiVersions);

    builder.IgniteApiVersioning(options =>
        options.DefaultApiVersion = apiVersions.Order().Last());

    var openApiDocs = new List<(string Name, ApiVersion Version)> { (Name: "latest", Version: apiVersions.Last()) }
        .Concat(apiVersions.OrderDescending().Select(v => (Name: $"v{v}", Version: v))).ToList();
    openApiDocs.ForEach(doc => builder.Services.AddOpenApiDocument((settings, provider) =>
    {
        settings.Title = provider.GetRequiredService<IHostEnvironment>().ApplicationName;
        settings.DocumentName = doc.Name;
        settings.Version = $"v{doc.Version}";
        settings.ApiGroupNames = [settings.Version];
        settings.SchemaSettings.SchemaNameGenerator = new TypeToStringSchemaNameGenerator();
        settings.SchemaSettings.GenerateEnumMappingDescription = true;
    }));

    #endregion

    // Add health checks UI
    //builder.IgniteHealthChecksUi();


    //SqlServerDbContext
    builder.IgniteSqlServerDbContextFactory<SimpleDbContext>(
        configureDbContextOptionsBuilder: dbContextOptionsBuilder =>
        {
            dbContextOptionsBuilder.ConfigureWarnings(w => w.Ignore(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
        },
        configureSqlServerDbContextOptionsBuilder: sqlServerDbContextOptionsBuilder =>
        {
            sqlServerDbContextOptionsBuilder.MigrationsAssembly(
                typeof(SimpleDbContextDesignTimeFactory).Assembly.FullName);
        });
    //DbContext Migrations
    builder.AddDbContextMigrationsTask<SimpleDbContext>();
    //Sql Server Client
    builder.IgniteSqlServerClientFactory(nameof(SimpleDbContext));

    // Add Storage
    builder.IgniteAzureBlobServiceClient();
    builder.IgniteAzureQueueServiceClient();
    builder.IgniteAzureTableServiceClient();
    // Add Redis
    builder.IgniteRedisClient();


    builder.Services.AddHostedService<TestHostedService>();
    builder.Services.AddScoped<IValidator<TestRequest>, TestValidator>();
    builder.Services.AddScoped<IValidator<TestComplexRequest>, TestComplexRequestValidator>();


    builder.Services.AddOutboxMessageType<OutboxTestMessage>();
    builder.Services.AddOutboxDeliveryService<SimpleDbContext, OutboxMessageHandler>();

    builder.Services.AddOpenTelemetry().WithTracing(traceBuilder =>
        traceBuilder.AddTransactionalOutboxInstrumentation());


    var app = builder.Build();
    app.Ignite();

    app.IgniteNSwag();

    var root = app
        .MapGroup("v{version:apiVersion}")
        .AddFluentValidationAutoValidation()
        .WithApiVersionSet(app.NewApiVersionSet()
            .ReportApiVersions()
            .Build());

    root.MapGet("test", (IServiceProvider serviceProvider) =>
    {
        var dbContext = serviceProvider.GetRequiredService<SimpleDbContext>();
        //using var tx = dbContext.Database.BeginTransaction();
        dbContext.AddOutboxMessage(new OutboxTestMessage("test"));
        dbContext.SaveChanges();
        //Task.Delay(5000).Wait();
        //tx.Commit();
        return Results.Ok();
    });

    //app.IgniteHealthChecksUi();

    await app.RunAsync();
    return 0;
});