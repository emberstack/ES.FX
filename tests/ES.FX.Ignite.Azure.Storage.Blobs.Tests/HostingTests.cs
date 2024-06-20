using Azure.Storage.Blobs;
using ES.FX.Ignite.Azure.Storage.Blobs.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Storage.Blobs.Tests;

public class HostingTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("default", null)]
    [InlineData("default", "keyed")]
    public void CanAdd(string? name, string? serviceKey)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>(
                $"{AzureBlobStorageSpark.ConfigurationSectionPath}{(string.IsNullOrWhiteSpace(name) ? string.Empty : $":{name}")}:ConnectionString",
                "UseDevelopmentStorage=true;")
        ]);

        builder.IgniteAzureBlobServiceClient(name, serviceKey);

        var app = builder.Build();
        var client = app.Services.GetKeyedService<BlobServiceClient>(serviceKey);
        NotNull(client);

        var factory = app.Services.GetRequiredService<IAzureClientFactory<BlobServiceClient>>();
        NotNull(factory);

        client = factory.CreateClient(serviceKey ?? "Default");
        NotNull(client);
    }
}