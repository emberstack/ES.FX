using ES.FX.NousResearch.HermesAgent.Configuration;

namespace ES.FX.NousResearch.HermesAgent.Tests.Configuration;

public class HermesAgentClientOptionsTests
{
    [Fact]
    public void GetBaseAddress_Appends_Missing_Trailing_Slash()
    {
        var options = new HermesAgentClientOptions { BaseUrl = "http://localhost:8642" };

        Assert.Equal(new Uri("http://localhost:8642/"), options.GetBaseAddress());
    }

    [Fact]
    public void GetBaseAddress_Keeps_Existing_Trailing_Slash()
    {
        var options = new HermesAgentClientOptions { BaseUrl = "http://localhost:8642/" };

        Assert.Equal(new Uri("http://localhost:8642/"), options.GetBaseAddress());
    }

    [Fact]
    public void GetBaseAddress_Preserves_A_Path_Prefix_And_Appends_Slash()
    {
        // A reverse-proxied server under a path prefix must keep the prefix AND get the trailing slash so
        // relative request URIs (e.g. "v1/models") compose under it instead of replacing the last segment.
        var options = new HermesAgentClientOptions { BaseUrl = "https://gateway.example.com/hermes" };

        var baseAddress = options.GetBaseAddress();

        Assert.Equal(new Uri("https://gateway.example.com/hermes/"), baseAddress);
        Assert.Equal("https://gateway.example.com/hermes/v1/models", new Uri(baseAddress, "v1/models").ToString());
    }

    [Fact]
    public void GetBaseAddress_Throws_When_BaseUrl_Is_Missing()
    {
        var options = new HermesAgentClientOptions();

        Assert.Throws<InvalidOperationException>(() => options.GetBaseAddress());
    }
}
