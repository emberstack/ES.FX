using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using ES.FX.Ignite.Asp.Versioning.Hosting;
using Microsoft.Extensions.Hosting;

namespace ES.FX.Ignite.Asp.Versioning.Tests;

public class HostingTests
{
    [Fact]
    public void ServicesAdded()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(ApiVersion));
        Assert.DoesNotContain(builder.Services, s => s.ServiceType == typeof(IApiVersionDescriptionProviderFactory));

        builder.IgniteApiVersioning();

        builder.Build();
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(ApiVersion));
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(IApiVersionDescriptionProviderFactory));
    }
}