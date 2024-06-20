using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Interceptors;
using HealthChecks.UI.Data;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Moq;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests.Interceptors;

public class IPv6LoopbackAddressInterceptorTests
{
    [Fact]
    public async Task OnCollectExecuting_ShouldFixRelativeHealthCheckAddresses_WhenUsingIPv6LoopbackAddress()
    {
        var server = new Mock<IServer>();
        var addressFeature = new Mock<IServerAddressesFeature>();
        addressFeature.Setup(f => f.Addresses).Returns(["http://[::]"]);
        server.Setup(s => s.Features.Get<IServerAddressesFeature>()).Returns(addressFeature.Object);

        var interceptor = new IPv6LoopbackAddressInterceptor(server.Object);
        var configuration = new HealthCheckConfiguration { Uri = "/health" };

        await interceptor.OnCollectExecuting(configuration);

        Assert.Equal("http://[::1]/health", configuration.Uri);
    }

    [Fact]
    public async Task OnCollectExecuting_ShouldIgnoreNonIP6OrCorrectURI()
    {
        var uri = "http://192.168.1.1/health";
        var server = new Mock<IServer>();
        var addressFeature = new Mock<IServerAddressesFeature>();

        var interceptor = new IPv6LoopbackAddressInterceptor(server.Object);
        var configuration = new HealthCheckConfiguration { Uri = uri };

        await interceptor.OnCollectExecuting(configuration);

        Assert.Equal(uri, configuration.Uri);
    }
}