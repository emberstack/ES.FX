using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The full-view transform: the complete Zendesk object minus <c>url</c> API self-links (with
///     <c>html_url</c> always kept), null-valued properties, and absolute pagination URL strings.
/// </summary>
public class ZendeskLeanFullViewTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Strips_Url_Self_Links_At_Every_Depth_But_Keeps_The_Html_Permalink()
    {
        var view = ZendeskLean.ToFullView(Parse(
            """
            {"ticket":{"id":1,"url":"https://acme.zendesk.com/api/v2/tickets/1.json",
             "html_url":"https://acme.zendesk.com/agent/tickets/1",
             "fields":[{"id":9,"value":"x","url":"https://acme.zendesk.com/api/v2/ticket_fields/9.json"}]}}
            """));

        var ticket = view.GetProperty("ticket");
        Assert.False(ticket.TryGetProperty("url", out _));
        Assert.Equal("https://acme.zendesk.com/agent/tickets/1", ticket.GetProperty("html_url").GetString());
        Assert.False(ticket.GetProperty("fields")[0].TryGetProperty("url", out _)); // nested self-link gone too
        Assert.Equal("x", ticket.GetProperty("fields")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void Strips_Null_Valued_Properties_Recursively()
    {
        var view = ZendeskLean.ToFullView(Parse(
            """
            {"ticket":{"id":1,"assignee_id":null,"satisfaction_rating":null,
             "via":{"channel":"web","source":{"rel":null}}}}
            """));

        var ticket = view.GetProperty("ticket");
        Assert.False(ticket.TryGetProperty("assignee_id", out _));
        Assert.False(ticket.TryGetProperty("satisfaction_rating", out _));
        Assert.False(ticket.GetProperty("via").GetProperty("source").TryGetProperty("rel", out _));
        Assert.Equal("web", ticket.GetProperty("via").GetProperty("channel").GetString());
    }

    [Fact]
    public void Strips_Pagination_Url_Strings_And_Links_Blocks_But_Not_Page_Numbers()
    {
        var view = ZendeskLean.ToFullView(Parse(
            """
            {"tickets":[{"id":1}],"count":3,
             "next_page":"https://acme.zendesk.com/api/v2/tickets.json?page=2","previous_page":null,
             "links":{"next":"https://acme.zendesk.com/api/v2/tickets?page[after]=x","prev":null}}
            """));

        Assert.False(view.TryGetProperty("next_page", out _));
        Assert.False(view.TryGetProperty("previous_page", out _));
        Assert.False(view.TryGetProperty("links", out _));
        Assert.Equal(3, view.GetProperty("count").GetInt32());

        // A NUMERIC next_page (our own computed envelope field) is not a Zendesk URL and survives.
        var numeric = ZendeskLean.ToFullView(Parse("""{"next_page":2}"""));
        Assert.Equal(2, numeric.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public void Preserves_Arrays_Positionally_Including_Null_Entries()
    {
        var view = ZendeskLean.ToFullView(Parse("""{"values":["a",null,3]}"""));

        var values = view.GetProperty("values");
        Assert.Equal(3, values.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, values[1].ValueKind);
    }

    [Fact]
    public void Leaves_Scalars_And_NonObject_Roots_Untouched()
    {
        Assert.Equal(42, ZendeskLean.ToFullView(Parse("42")).GetInt32());
        Assert.Equal("text", ZendeskLean.ToFullView(Parse("\"text\"")).GetString());
    }
}