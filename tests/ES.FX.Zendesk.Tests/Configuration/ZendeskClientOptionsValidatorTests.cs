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

    [Theory]
    [InlineData("acme")]
    [InlineData("acme-support")]
    [InlineData("d3v1")]
    [InlineData("a")]
    public void Valid_Subdomain_Shapes_Pass(string subdomain)
    {
        var options = Valid();
        options.Subdomain = subdomain;

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("acme.evil.com")]
    [InlineData("bad host")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("acme/path")]
    public void Invalid_Subdomain_Shapes_Fail(string subdomain)
    {
        var options = Valid();
        options.Subdomain = subdomain;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Subdomain_Is_Not_Validated_When_BaseUrl_Takes_Precedence()
    {
        var options = Valid();
        options.Subdomain = "not a subdomain";
        options.BaseUrl = "https://sandbox.example.com/";

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://files.example.com")]
    public void Invalid_BaseUrl_Fails(string baseUrl)
    {
        var options = Valid();
        options.Subdomain = string.Empty;
        options.BaseUrl = baseUrl;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Valid_BaseUrl_Without_Subdomain_Passes()
    {
        var options = Valid();
        options.Subdomain = string.Empty;
        options.BaseUrl = "https://sandbox.example.com";

        Assert.True(_validator.Validate(null, options).Succeeded);
    }
}