using Microsoft.AspNetCore.Mvc.Testing;

namespace ES.FX.Ignite.NSwag.Tests.Hosting;

public class NSwagFunctionalTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Swagger_Accessible()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/swagger/");
        Assert.True(response.IsSuccessStatusCode);
    }
}