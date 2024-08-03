using System.Text;
using ES.FX.Ignite.FluentValidation.Tests.SUT.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;

namespace ES.FX.Ignite.FluentValidation.Tests.Functional;

public class FluentValidationFunctionalTests(WebApplicationFactory<Program> factory)
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
                JsonConvert.SerializeObject(new SimpleValidationEndpoint.Request(name)),
                Encoding.UTF8, "application/json"));
        var resultContent = await response.Content.ReadAsStringAsync();

        Assert.True(resultContent.Contains("validation error") == shouldThrowValidationError);
    }
}