using ES.FX.Additions.Serilog.Sinks.Console;

namespace ES.FX.Additions.Serilog.Tests;

public class ConsoleOutputTemplatesTests
{
    [Fact]
    public void Default_IsNotNullOrWhitespace()
    {
        Assert.False(string.IsNullOrWhiteSpace(ConsoleOutputTemplates.Default));
    }

    [Theory]
    [InlineData("{Timestamp")]
    [InlineData("{Level:u3}")]
    [InlineData("{SourceContext}")]
    [InlineData("{Message:lj}")]
    [InlineData("{NewLine}")]
    [InlineData("{Exception}")]
    public void Default_ContainsExpectedTokens(string token)
    {
        Assert.Contains(token, ConsoleOutputTemplates.Default);
    }
}
