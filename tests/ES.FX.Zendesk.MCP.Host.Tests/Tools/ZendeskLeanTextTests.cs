using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskLeanTextTests
{
    [Fact]
    public void Truncate_Returns_A_Fitting_Value_Unchanged()
    {
        Assert.Equal("short", ZendeskLean.Truncate("short", 5));
        Assert.Equal("", ZendeskLean.Truncate("", 10));
    }

    [Fact]
    public void Truncate_Cuts_With_A_Plain_Trailing_Ellipsis()
    {
        Assert.Equal("abc…", ZendeskLean.Truncate("abcdef", 3));
    }

    [Fact]
    public void Truncate_Never_Splits_A_Surrogate_Pair()
    {
        // "💇" is a surrogate pair; a cut at maxChars=3 would land between its two chars.
        Assert.Equal("aa…", ZendeskLean.Truncate("aa💇z", 3));
    }

    [Fact]
    public void TruncateWithMarker_Returns_A_Fitting_Value_Unchanged()
    {
        Assert.Equal("short", ZendeskLean.TruncateWithMarker("short", 10, "irrelevant"));
    }

    [Fact]
    public void TruncateWithMarker_Emits_The_SelfDescribing_Marker()
    {
        var truncated = ZendeskLean.TruncateWithMarker("abcdefghij", 4, "re-call with maxBodyChars:0");

        // The exact contract style: "…[truncated N chars — {recovery}]".
        Assert.Equal("abcd…[truncated 6 chars — re-call with maxBodyChars:0]", truncated);
    }

    [Fact]
    public void HtmlToPlainText_Converts_Blocks_Entities_And_Collapses_Whitespace()
    {
        var text = ZendeskLean.HtmlToPlainText(
            "<h1>Title</h1><p>Hello   <b>world</b></p><p>Second&nbsp;para &amp; more</p>");

        Assert.Equal("Title\n\nHello world\n\nSecond para & more", text);
    }

    [Fact]
    public void HtmlToPlainText_Drops_Script_And_Style_Blocks_With_Their_Content()
    {
        var text = ZendeskLean.HtmlToPlainText(
            "<style>p { color: red; }</style><p>Visible</p><script>var hidden = 1;</script>");

        Assert.Equal("Visible", text);
    }

    [Fact]
    public void HtmlToPlainText_Turns_Line_Breaks_And_List_Items_Into_Newlines()
    {
        Assert.Equal("first\nsecond", ZendeskLean.HtmlToPlainText("<div>first<br/>second</div>"));
        Assert.Equal("one\n\ntwo", ZendeskLean.HtmlToPlainText("<ul><li>one</li><li>two</li></ul>"));
    }

    [Fact]
    public void HtmlToPlainText_Decodes_Entities_After_Stripping_So_Literal_Markup_Text_Survives()
    {
        // "&lt;b&gt;" is TEXT about a tag, not a tag — it must survive as "<b>".
        Assert.Equal("use <b> for bold", ZendeskLean.HtmlToPlainText("<p>use &lt;b&gt; for bold</p>"));
    }

    [Fact]
    public void HtmlToPlainText_Handles_Empty_And_Plain_Input()
    {
        Assert.Equal(string.Empty, ZendeskLean.HtmlToPlainText(string.Empty));
        Assert.Equal("no markup here", ZendeskLean.HtmlToPlainText("no markup here"));
    }
}