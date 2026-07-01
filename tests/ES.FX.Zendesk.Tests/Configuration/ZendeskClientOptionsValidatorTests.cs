using ES.FX.Zendesk.Configuration;

namespace ES.FX.Zendesk.Tests.Configuration;

public class ZendeskClientOptionsValidatorTests
{
    private readonly ZendeskClientOptionsValidator _validator = new();

    private static ZendeskClientOptions Valid() => new()
    {
        Subdomain = "acme",
        OAuth = new ZendeskOAuthOptions { ClientId = "cid", ClientSecret = "secret" }
    };

    [Fact]
    public void Valid_OAuth_Configuration_Passes()
    {
        Assert.True(_validator.Validate(null, Valid()).Succeeded);
    }

    [Fact]
    public void Missing_Subdomain_And_BaseUrl_Fails()
    {
        var options = Valid();
        options.Subdomain = string.Empty;
        options.BaseUrl = null;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Missing_ClientId_Fails()
    {
        var options = Valid();
        options.OAuth.ClientId = null;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Missing_ClientSecret_Fails()
    {
        var options = Valid();
        options.OAuth.ClientSecret = null;

        Assert.True(_validator.Validate(null, options).Failed);
    }
}