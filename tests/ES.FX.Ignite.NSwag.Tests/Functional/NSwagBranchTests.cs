using ES.FX.Ignite.NSwag.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSwag.AspNetCore;

namespace ES.FX.Ignite.NSwag.Tests.Functional;

/// <summary>
///     Drives <see cref="NSwagHostingExtensions.IgniteNSwag" /> across its full parameter surface using an
///     in-process <see cref="TestServer" />. Each test builds its own <see cref="WebApplication" /> so that the
///     individual branches (useSwaggerUi, dark mode, the configure delegate, and the served OpenAPI document)
///     can be exercised independently — something the SUT's fixed <c>Program.cs</c> cannot do.
/// </summary>
public class NSwagBranchTests
{
    private static async Task<WebApplication> BuildAppAsync(
        bool useSwaggerUi = true,
        bool useSwaggerUiDarkMode = true,
        string applicationName = "TestApp",
        Action<SwaggerUiSettings>? configureSwaggerUiSettings = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = applicationName,
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();

        // NSwag needs a registered OpenAPI document generator (and the API Explorer
        // that discovers minimal-API endpoints) to serve the spec.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApiDocument();

        var app = builder.Build();

        app.IgniteNSwag(
            useSwaggerUi: useSwaggerUi,
            useSwaggerUiDarkMode: useSwaggerUiDarkMode,
            configureSwaggerUiSettings: configureSwaggerUiSettings);

        // A minimal endpoint so the generated document has at least one path.
        app.MapGet("/ping", () => "pong");

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.GetTestServer();
        // NSwag's UI root (/swagger/) 302-redirects to /swagger/index.html; follow it so we can
        // assert against the rendered UI HTML rather than the redirect shell.
        var handler = server.CreateHandler();
        return new HttpClient(new RedirectFollowingHandler(handler))
        {
            BaseAddress = new Uri("http://localhost/")
        };
    }

    /// <summary>
    ///     The TestServer handler does not follow redirects on its own. This wraps it so that a single
    ///     redirect (the /swagger/ -> /swagger/index.html hop) is transparently followed.
    /// </summary>
    private sealed class RedirectFollowingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var redirects = 0;
            while ((response.StatusCode == System.Net.HttpStatusCode.Found ||
                    response.StatusCode == System.Net.HttpStatusCode.MovedPermanently) &&
                   response.Headers.Location is not null &&
                   redirects++ < 5)
            {
                var location = response.Headers.Location;
                var target = location.IsAbsoluteUri
                    ? location
                    : new Uri(request.RequestUri!, location);
                response.Dispose();
                var followUp = new HttpRequestMessage(HttpMethod.Get, target);
                response = await base.SendAsync(followUp, cancellationToken);
            }

            return response;
        }
    }

    [Fact]
    public async Task OpenApiDocument_IsServedAndValid()
    {
        await using var app = await BuildAppAsync();
        var client = CreateClient(app);

        var response = await client.GetAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Core structural markers of an OpenAPI document produced by NSwag.
        Assert.Contains("\"openapi\"", json);
        Assert.Contains("\"paths\"", json);
        // The endpoint we mapped must be present in the generated spec.
        Assert.Contains("/ping", json);
    }

    [Fact]
    public async Task UseSwaggerUiFalse_UiIsAbsentButDocumentStillServed()
    {
        await using var app = await BuildAppAsync(useSwaggerUi: false);
        var client = CreateClient(app);

        // UI must NOT be wired up.
        var uiResponse = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);

        // The OpenAPI document endpoint must still work (UseOpenApi always runs).
        var docResponse = await client.GetAsync(
            "/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.True(docResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UseSwaggerUiTrue_UiIsServed()
    {
        await using var app = await BuildAppAsync(useSwaggerUi: true);
        var client = CreateClient(app);

        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DarkModeTrue_InjectsDarkThemeCss()
    {
        await using var app = await BuildAppAsync(useSwaggerUiDarkMode: true);
        var client = CreateClient(app);

        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The dark theme is injected via CustomInlineStyles -> an inline <style> block.
        // SwaggerThemes' UniversalDark CSS overrides the .swagger-ui background/colors.
        Assert.Contains("<style", html);
        // The theme redefines swagger-ui CSS variables/selectors; assert a dark-theme marker.
        var expectedCss = SwaggerThemes.SwaggerTheme.GetSwaggerThemeCss(SwaggerThemes.Theme.UniversalDark);
        // Grab a stable, non-trivial fragment of the theme CSS and assert it is present verbatim.
        var fragment = ExtractStableCssFragment(expectedCss);
        Assert.Contains(fragment, html);
    }

    [Fact]
    public async Task DarkModeFalse_DoesNotInjectDarkThemeCss()
    {
        await using var app = await BuildAppAsync(useSwaggerUiDarkMode: false);
        var client = CreateClient(app);

        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var expectedCss = SwaggerThemes.SwaggerTheme.GetSwaggerThemeCss(SwaggerThemes.Theme.UniversalDark);
        var fragment = ExtractStableCssFragment(expectedCss);
        Assert.DoesNotContain(fragment, html);
    }

    [Fact]
    public async Task Defaults_ApplyDocumentTitleFromApplicationName()
    {
        await using var app = await BuildAppAsync(applicationName: "MyNSwagApp");
        var client = CreateClient(app);

        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // settings.DocumentTitle = "{ApplicationName} - Swagger UI" -> rendered into the <title>.
        Assert.Contains("MyNSwagApp - Swagger UI", html);
    }

    [Fact]
    public async Task Defaults_ApplyDocExpansionAndDisplayRequestDuration()
    {
        await using var app = await BuildAppAsync();
        var client = CreateClient(app);

        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The SwaggerUI config is rendered inline as a JS object literal (unquoted keys).
        Assert.Contains("docExpansion: \"list\"", html);
        Assert.Contains("displayRequestDuration: true", html);
    }

    [Fact]
    public async Task ConfigureDelegate_IsInvokedWithSettings()
    {
        var invoked = false;
        SwaggerUiSettings? captured = null;

        await using var app = await BuildAppAsync(configureSwaggerUiSettings: settings =>
        {
            invoked = true;
            captured = settings;
        });

        // Force UI rendering so the settings pipeline runs.
        var client = CreateClient(app);
        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.True(invoked, "configureSwaggerUiSettings delegate was never invoked.");
        Assert.NotNull(captured);
    }

    [Fact]
    public async Task ConfigureDelegate_RunsAfterDefaults_SoConsumerCanOverride()
    {
        await using var app = await BuildAppAsync(
            applicationName: "ShouldBeOverridden",
            configureSwaggerUiSettings: settings =>
            {
                // Override a value that the built-in defaults already set.
                settings.DocumentTitle = "Overridden Title";
                settings.DocExpansion = "full";
            });

        var client = CreateClient(app);
        var response = await client.GetAsync("/swagger/", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The delegate runs LAST, so its values win over the defaults.
        Assert.Contains("Overridden Title", html);
        Assert.DoesNotContain("ShouldBeOverridden - Swagger UI", html);
        Assert.Contains("docExpansion: \"full\"", html);
        Assert.DoesNotContain("docExpansion: \"list\"", html);
    }

    /// <summary>
    ///     Returns a stable, distinctive, non-trivial substring taken from the START of the theme CSS.
    ///     The UniversalDark theme opens with a <c>@media (prefers-color-scheme: dark)</c> block that the
    ///     default (non-dark) SwaggerUI page does not emit, making this a reliable presence/absence marker
    ///     (a generic <c>.swagger-ui</c> selector would also occur in the standard bundle CSS).
    /// </summary>
    private static string ExtractStableCssFragment(string css)
    {
        var normalized = css.TrimStart();
        Assert.StartsWith("@media (prefers-color-scheme: dark)", normalized);
        // Take a long, distinctive leading chunk of the actual theme CSS.
        return normalized[..Math.Min(120, normalized.Length)];
    }
}
