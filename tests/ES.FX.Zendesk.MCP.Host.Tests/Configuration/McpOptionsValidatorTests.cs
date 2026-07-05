using ES.FX.Zendesk.MCP.Host.Configuration;
using Microsoft.Extensions.Configuration;

namespace ES.FX.Zendesk.MCP.Host.Tests.Configuration;

public class McpOptionsValidatorTests
{
    [Fact]
    public void Defaults_Are_Valid_And_Resolve_To_The_Global_Budget()
    {
        var options = new McpOptions();

        Assert.True(new McpOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(60_000, options.Tools.MaxResponseChars);
        Assert.Equal(60_000, options.Tools.GetMaxResponseChars("tickets_list"));
    }

    [Fact]
    public void A_PerTool_Override_Wins_Over_The_Global_Budget_Case_Insensitively()
    {
        var options = new McpOptions
        {
            Tools = new McpToolsOptions
            {
                MaxResponseChars = 60_000,
                MaxResponseCharsByTool = { ["tickets_audits_list"] = 90_000 }
            }
        };

        Assert.Equal(90_000, options.Tools.GetMaxResponseChars("tickets_audits_list"));
        Assert.Equal(90_000, options.Tools.GetMaxResponseChars("TICKETS_AUDITS_LIST"));
        Assert.Equal(60_000, options.Tools.GetMaxResponseChars("tickets_list"));
    }

    [Fact]
    public void Binding_From_Configuration_Populates_The_Budget_And_Its_Overrides()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mcp:Tools:MaxResponseChars"] = "45000",
            ["Mcp:Tools:MaxResponseCharsByTool:tickets_audits_list"] = "90000"
        }).Build();
        var options = new McpOptions();

        configuration.GetSection(McpOptions.SectionKey).Bind(options);

        Assert.Equal(45_000, options.Tools.MaxResponseChars);
        Assert.Equal(90_000, options.Tools.GetMaxResponseChars("tickets_audits_list"));
        // Binding must not replace the dictionary with a case-SENSITIVE one.
        Assert.Equal(90_000, options.Tools.GetMaxResponseChars("Tickets_Audits_List"));
    }

    [Fact]
    public void A_Global_Budget_Below_The_Minimum_Fails_Naming_The_Key()
    {
        var options = new McpOptions { Tools = new McpToolsOptions { MaxResponseChars = 999 } };

        var result = new McpOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Mcp:Tools:MaxResponseChars", result.FailureMessage);
        Assert.Contains("1000", result.FailureMessage);
        Assert.Contains("999", result.FailureMessage);
    }

    [Fact]
    public void A_PerTool_Override_Below_The_Minimum_Fails_Naming_The_Tool_Key()
    {
        var options = new McpOptions
        {
            Tools = new McpToolsOptions { MaxResponseCharsByTool = { ["tickets_list"] = 10 } }
        };

        var result = new McpOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Mcp:Tools:MaxResponseCharsByTool:tickets_list", result.FailureMessage);
    }
}