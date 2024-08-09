using Azure.Security.KeyVault.Secrets;
using ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Xunit.Assert;

namespace ES.FX.Ignite.Azure.Security.KeyVault.Secrets.Tests;

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
                $"{AzureKeyVaultSecretsSpark.ConfigurationSectionPath}{(string.IsNullOrWhiteSpace(name) ? string.Empty : $":{name}")}:VaultUri",
                "https://vaulturi")
        ]);

        builder.IgniteAzureKeyVaultSecretClient(name, serviceKey);

        var app = builder.Build();
        var client = app.Services.GetKeyedService<SecretClient>(serviceKey);
        NotNull(client);

        var factory = app.Services.GetRequiredService<IAzureClientFactory<SecretClient>>();
        NotNull(factory);

        client = factory.CreateClient(serviceKey ?? "Default");
        NotNull(client);
    }
}