using System.Net;
using ES.FX.Ignite.Swashbuckle.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.Ignite.Swashbuckle.Tests.Functional;

/// <summary>
///     End-to-end coverage of the <see cref="Swashbuckle" /> middleware wiring driven by the
///     <c>SwaggerEnabled</c> / <c>SwaggerUIEnabled</c> settings. The SUT host binds settings from
///     configuration (default section <c>Ignite:Swashbuckle:Settings</c>), so each test flips the flags via
///     injected in-memory configuration and asserts the resulting endpoints.
/// </summary>
public class SwashbuckleEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SwaggerUiPath = "/swagger/";
    private const string SwaggerDocumentPath = "/swagger/v1/swagger.json";

    private WebApplicationFactory<Program> FactoryWithSettings(bool? swaggerEnabled, bool? swaggerUiEnabled) =>
        factory.WithWebHostBuilder(webHost =>
        {
            // UseSetting values are surfaced into the app's ConfigurationManager, so they are visible to
            // the SUT's pre-build builder.IgniteSwashbuckle() call (unlike ConfigureAppConfiguration, whose
            // deferred callbacks run after the settings singleton is already registered).
            if (swaggerEnabled is not null)
                webHost.UseSetting("Ignite:Swashbuckle:Settings:SwaggerEnabled",
                    swaggerEnabled.Value ? "true" : "false");
            if (swaggerUiEnabled is not null)
                webHost.UseSetting("Ignite:Swashbuckle:Settings:SwaggerUIEnabled",
                    swaggerUiEnabled.Value ? "true" : "false");
        });

    [Fact]
    public async Task BothEnabled_ByDefault_UiAndDocumentAreServed()
    {
        var client = factory.CreateClient();

        var uiResponse = await client.GetAsync(SwaggerUiPath, TestContext.Current.CancellationToken);
        var docResponse = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);

        Assert.True(uiResponse.IsSuccessStatusCode);
        Assert.True(docResponse.IsSuccessStatusCode);

        // Confirm the document endpoint returns a real OpenAPI document, not just any 200.
        var body = await docResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("openapi", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerUiEnabledFalse_UiReturns404_ButDocumentStillServed()
    {
        using var scopedFactory = FactoryWithSettings(true, false);
        var client = scopedFactory.CreateClient();

        var uiResponse = await client.GetAsync(SwaggerUiPath, TestContext.Current.CancellationToken);
        var docResponse = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.True(docResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SwaggerEnabledFalse_DocumentReturns404()
    {
        using var scopedFactory = FactoryWithSettings(false, true);
        var client = scopedFactory.CreateClient();

        var docResponse = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, docResponse.StatusCode);
    }

    [Fact]
    public async Task BothDisabled_UiAndDocumentReturn404()
    {
        using var scopedFactory = FactoryWithSettings(false, false);
        var client = scopedFactory.CreateClient();

        // Sanity: the bound settings actually reflect the disabled flags in this host.
        using var scope = scopedFactory.Services.CreateScope();
        var settings = scope.ServiceProvider
            .GetRequiredService<SwashbuckleSparkSettings>();
        Assert.False(settings.SwaggerEnabled);
        Assert.False(settings.SwaggerUIEnabled);

        var uiResponse = await client.GetAsync(SwaggerUiPath, TestContext.Current.CancellationToken);
        var docResponse = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, docResponse.StatusCode);
    }
}