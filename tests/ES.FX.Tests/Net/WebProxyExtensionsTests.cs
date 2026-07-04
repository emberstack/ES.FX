using System.Net;
using ES.FX.Net;
using ES.FX.Net.Extensions;

namespace ES.FX.Tests.Net;

public class WebProxyExtensionsTests
{
    [Fact]
    public void BuildBasicHttpProxy_NullOptions_ReturnsNull()
    {
        BasicHttpProxyOptions? options = null;
        Assert.Null(options.BuildBasicHttpProxy());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildBasicHttpProxy_WhitespaceOrNullAddress_ReturnsNull(string? address)
    {
        var options = new BasicHttpProxyOptions { Address = address };
        Assert.Null(options.BuildBasicHttpProxy());
    }

    [Fact]
    public void BuildBasicHttpProxy_ValidAddress_BuildsWebProxy()
    {
        var options = new BasicHttpProxyOptions
        {
            Address = "http://proxy.example.com:8080",
            BypassProxyOnLocal = true,
            // WebProxy.BypassList entries are treated as regex patterns; use valid ones.
            BypassList = new[] { @".*\.local", "127.0.0.1" },
            UseDefaultCredentials = true
        };

        var proxy = Assert.IsType<WebProxy>(options.BuildBasicHttpProxy());
        Assert.Equal(new Uri("http://proxy.example.com:8080"), proxy.Address);
        Assert.True(proxy.BypassProxyOnLocal);
        Assert.Equal(new[] { @".*\.local", "127.0.0.1" }, proxy.BypassList);
        Assert.True(proxy.UseDefaultCredentials);
    }

    [Fact]
    public void BuildBasicHttpProxy_NullBypassList_DefaultsToEmpty()
    {
        var options = new BasicHttpProxyOptions
        {
            Address = "http://proxy.example.com:8080",
            BypassList = null
        };

        var proxy = Assert.IsType<WebProxy>(options.BuildBasicHttpProxy());
        Assert.NotNull(proxy.BypassList);
        Assert.Empty(proxy.BypassList);
    }

    [Fact]
    public void BuildBasicHttpProxy_NoCredentials_CredentialsNull()
    {
        var options = new BasicHttpProxyOptions
        {
            Address = "http://proxy.example.com:8080",
            Credentials = null
        };

        var proxy = Assert.IsType<WebProxy>(options.BuildBasicHttpProxy());
        Assert.Null(proxy.Credentials);
    }

    [Fact]
    public void BuildBasicHttpProxy_WithCredentials_SetsNetworkCredential()
    {
        var options = new BasicHttpProxyOptions
        {
            Address = "http://proxy.example.com:8080",
            Credentials = new NetworkCredentialOptions
            {
                UserName = "user",
                Password = "pass",
                Domain = "corp"
            }
        };

        var proxy = Assert.IsType<WebProxy>(options.BuildBasicHttpProxy());
        var credential = Assert.IsType<NetworkCredential>(proxy.Credentials);
        Assert.Equal("user", credential.UserName);
        Assert.Equal("pass", credential.Password);
        Assert.Equal("corp", credential.Domain);
    }
}