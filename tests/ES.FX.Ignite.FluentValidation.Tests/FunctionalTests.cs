using System.Text;
using System.Text.Json;
using ES.FX.Ignite.FluentValidation.Tests.SUT.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ES.FX.Ignite.FluentValidation.Tests;

public class FunctionalTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("", true)]
    [InlineData("test", false)]
    public async Task AutoValidation_Applied(string name, bool shouldThrowValidationError)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            SimpleValidationEndpoint.RoutePattern,
            new StringContent(
                JsonSerializer.Serialize(new SimpleValidationEndpoint.Request(name)),
                Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        var resultContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(resultContent.Contains("validation error") == shouldThrowValidationError);
    }
}