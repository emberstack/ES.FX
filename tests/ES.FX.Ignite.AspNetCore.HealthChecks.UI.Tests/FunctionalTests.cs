using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Configuration;
using ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests.SUT;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ES.FX.Ignite.AspNetCore.HealthChecks.UI.Tests;

public class FunctionalTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ApiPath_Accessible()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync(new HealthChecksUiSparkSettings().UiApiEndpointPath);
        Assert.True(response.IsSuccessStatusCode);
    }
}