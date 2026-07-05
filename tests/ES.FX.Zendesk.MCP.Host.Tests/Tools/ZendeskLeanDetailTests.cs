using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskLeanDetailTests
{
    [Theory]
    [InlineData("summary")]
    [InlineData("SUMMARY")]
    [InlineData("  summary  ")]
    [InlineData("concise")] // documented alias
    [InlineData("Concise")]
    public void ParseDetail_Accepts_Summary_And_Its_Alias(string detail) =>
        Assert.Equal(ZendeskDetail.Summary, ZendeskLean.ParseDetail(detail));

    [Theory]
    [InlineData("full")]
    [InlineData("FULL")]
    [InlineData("detailed")] // documented aliases
    [InlineData("verbose")]
    public void ParseDetail_Accepts_Full_And_Its_Aliases(string detail) =>
        Assert.Equal(ZendeskDetail.Full, ZendeskLean.ParseDetail(detail));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseDetail_Defaults_A_Missing_Value_To_Summary(string? detail) =>
        Assert.Equal(ZendeskDetail.Summary, ZendeskLean.ParseDetail(detail));

    [Theory]
    [InlineData("fill")] // near-miss typo must NOT be silently coerced
    [InlineData("all")]
    [InlineData("true")]
    public void ParseDetail_Rejects_Unknown_Values_Naming_The_Allowed_Ones(string detail)
    {
        var exception = Assert.Throws<McpException>(() => ZendeskLean.ParseDetail(detail));

        Assert.Contains(detail, exception.Message);
        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Contains("concise", exception.Message);
        Assert.Contains("detailed", exception.Message);
        Assert.Contains("verbose", exception.Message);
    }
}