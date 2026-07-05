using ES.FX.NousResearch.HermesAgent.Configuration;

namespace ES.FX.NousResearch.HermesAgent.Tests.Configuration;

public class HermesAgentClientOptionsValidatorTests
{
    private readonly HermesAgentClientOptionsValidator _validator = new();

    private static HermesAgentClientOptions Valid() => new()
    {
        BaseUrl = "http://localhost:8642",
        ApiKey = "0123456789abcdef0123456789abcdef"
    };

    [Fact]
    public void Valid_Configuration_Passes()
    {
        Assert.True(_validator.Validate(null, Valid()).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_BaseUrl_Fails(string? baseUrl)
    {
        var options = Valid();
        options.BaseUrl = baseUrl;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BaseUrl", result.FailureMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_ApiKey_Fails(string? apiKey)
    {
        var options = Valid();
        options.ApiKey = apiKey;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ApiKey", result.FailureMessage);
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("localhost:8642")]
    [InlineData("not a url")]
    [InlineData("ftp://files.example.com")]
    public void Relative_Or_Non_Http_BaseUrl_Fails(string baseUrl)
    {
        var options = Valid();
        options.BaseUrl = baseUrl;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("absolute http(s)", result.FailureMessage);
    }

    [Theory]
    [InlineData("http://localhost:8642")]
    [InlineData("https://hermes.example.com/")]
    [InlineData("https://gateway.example.com/hermes")]
    public void Absolute_Http_And_Https_BaseUrls_Pass(string baseUrl)
    {
        var options = Valid();
        options.BaseUrl = baseUrl;

        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void All_Failures_Are_Accumulated_Into_One_Result()
    {
        var options = new HermesAgentClientOptions();

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("BaseUrl", result.FailureMessage);
        Assert.Contains("ApiKey", result.FailureMessage);
    }
}