#pragma warning disable CS9113 // Parameter is unread.

using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Playground.Shared.Data.Simple.EntityFrameworkCore;

namespace Playground.Microservice.Api.Host.HostedServices;

internal class TestHostedService(
    ILogger<TestHostedService> logger,
    IServiceProvider serviceProvider,
    IDbContextFactory<SimpleDbContext> dbContextFactory,
    SimpleDbContext context,
    SqlConnection connection) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            await Task.Delay(2000);

            logger.LogInformation("Running");

            var writeFactory = serviceProvider.GetRequiredService<IDbContextFactory<SimpleDbContext>>();

            var writeContext = writeFactory.CreateDbContext();

            var writeUsers = writeContext.SimpleUsers.ToList();

            var contextUsers = context.SimpleUsers.ToList();

            var a = connection.State;
            try
            {
                var blobClient = serviceProvider.GetRequiredKeyedService<BlobServiceClient>(null);
                var containerclient = blobClient.GetBlobContainerClient("testcontainer");
                containerclient.CreateIfNotExists(PublicAccessType.Blob);


                var queueClient = serviceProvider.GetRequiredKeyedService<QueueServiceClient>(null);
                queueClient.CreateQueue("testqueue");

                var tableClient = serviceProvider.GetRequiredKeyedService<TableServiceClient>(null);
                tableClient.CreateTableIfNotExists("testTable");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error getting blob containers");
            }
        }
    }
}