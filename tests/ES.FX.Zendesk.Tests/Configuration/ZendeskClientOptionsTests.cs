using ES.FX.Zendesk.Configuration;

namespace ES.FX.Zendesk.Tests.Configuration;

public class ZendeskClientOptionsTests
{
    [Fact]
    public void GetOAuthTokenEndpoint_Derives_From_Subdomain()
    {
        var options = new ZendeskClientOptions { Subdomain = "acme" };

        Assert.Equal(new Uri("https://acme.zendesk.com/oauth/tokens"), options.GetOAuthTokenEndpoint());
    }

    [Fact]
    public void GetOAuthTokenEndpoint_Derives_From_BaseUrl_When_Subdomain_Is_Empty()
    {
        // The validator explicitly allows a BaseUrl-only configuration (sandbox / test double); the token
        // endpoint must resolve against that host instead of building an invalid subdomain URL.
        var options = new ZendeskClientOptions { BaseUrl = "https://sandbox.example.com/api/v2/" };

        Assert.Equal(new Uri("https://sandbox.example.com/oauth/tokens"), options.GetOAuthTokenEndpoint());
    }

    [Fact]
    public void GetOAuthTokenEndpoint_Prefers_BaseUrl_Over_Subdomain()
    {
        // Credentials must go to the same host the API calls target, never to a differently configured tenant.
        var options = new ZendeskClientOptions
        {
            Subdomain = "acme",
            BaseUrl = "http://localhost:5000/api/v2/"
        };

        Assert.Equal(new Uri("http://localhost:5000/oauth/tokens"), options.GetOAuthTokenEndpoint());
    }

    [Fact]
    public void GetOAuthTokenEndpoint_Prefers_An_Explicit_TokenEndpoint()
    {
        var options = new ZendeskClientOptions
        {
            Subdomain = "acme",
            OAuth = new ZendeskOAuthOptions { TokenEndpoint = new Uri("https://auth.example.com/token") }
        };

        Assert.Equal(new Uri("https://auth.example.com/token"), options.GetOAuthTokenEndpoint());
    }

    [Fact]
    public void GetBaseAddress_Prefers_BaseUrl_And_Ensures_Trailing_Slash()
    {
        var options = new ZendeskClientOptions { Subdomain = "acme", BaseUrl = "https://sandbox.example.com/api/v2" };

        Assert.Equal(new Uri("https://sandbox.example.com/api/v2/"), options.GetBaseAddress());
    }

    [Fact]
    public void GetBaseAddress_Derives_From_Subdomain()
    {
        var options = new ZendeskClientOptions { Subdomain = "acme" };

        Assert.Equal(new Uri("https://acme.zendesk.com/api/v2/"), options.GetBaseAddress());
    }
}