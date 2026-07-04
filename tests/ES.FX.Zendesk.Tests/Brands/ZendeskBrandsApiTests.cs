using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Brands;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Brands;

public class ZendeskBrandsApiTests
{
    private static ZendeskBrandsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskBrandsApi>.Instance);

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "brands": [ { "id": 1, "name": "Acme", "subdomain": "acme", "default": true } ], "meta": { "has_more": false } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(100, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Brands);
        Assert.Equal("Acme", result.Brands[0].Name);
        Assert.True(result.Brands[0].Default);
        Assert.Equal("/api/v2/brands.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("page[size]=100", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "brand": { "id": 5, "name": "Acme Pro", "has_help_center": true } }""");
        var api = CreateApi(stub);

        var brand = await api.GetByIdAsync(5, TestContext.Current.CancellationToken);

        Assert.Equal("Acme Pro", brand.Name);
        Assert.True(brand.HasHelpCenter);
        Assert.Equal("https://acme.zendesk.com/api/v2/brands/5.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task Create_Update_Delete_Use_Brand_Envelope()
    {
        var createStub = new StubHttpMessageHandler("""{ "brand": { "id": 5, "name": "Acme Pro" } }""");
        var created = await CreateApi(createStub).CreateAsync(
            new ZendeskBrandWrite { Name = "Acme Pro", Subdomain = "acmepro" },
            TestContext.Current.CancellationToken);
        Assert.Equal(5, created.Id);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Contains("\"brand\":{\"name\":\"Acme Pro\",\"subdomain\":\"acmepro\"}", createStub.LastRequestBody);

        var updateStub = new StubHttpMessageHandler("""{ "brand": { "id": 5, "active": false } }""");
        await CreateApi(updateStub).UpdateAsync(5, new ZendeskBrandWrite { Active = false },
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/brands/5.json", updateStub.LastRequest!.RequestUri!.AbsolutePath);

        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(5, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
    }
}