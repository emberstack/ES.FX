using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The uniform list envelope: metadata first, one continuation kind per pagination regime, sideloads
///     summary-projected under their native names, and visible-failure semantics for anything without a shape.
/// </summary>
public class ZendeskLeanEnvelopeTests
{
    private const int DefaultBudget = 60_000;

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    private static string[] PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).ToArray();

    [Fact]
    public void Cursor_Envelope_Is_Metadata_First_With_After_Cursor()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """
                {"tickets":[{"id":1,"subject":"a","custom_fields":[{"id":9}]},{"id":2,"subject":"b"}],
                 "meta":{"has_more":true,"after_cursor":"xxx","before_cursor":"yyy"},
                 "links":{"next":"https://acme.zendesk.com/api/v2/tickets?page[after]=xxx","prev":null}}
                """),
            "tickets", ZendeskDetail.Summary, DefaultBudget);

        // Metadata first, items last — and NO next_page on a cursor tool (one continuation kind per tool).
        Assert.Equal(new[] { "detail", "has_more", "after_cursor", "items" }, PropertyNames(envelope));
        Assert.Equal("summary", envelope.GetProperty("detail").GetString());
        Assert.True(envelope.GetProperty("has_more").GetBoolean());
        Assert.Equal("xxx", envelope.GetProperty("after_cursor").GetString());
        var items = envelope.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.False(items[0].TryGetProperty("custom_fields", out _)); // summary rows, not raw rows
    }

    [Fact]
    public void Cursor_Envelope_Omits_The_Cursor_When_There_Is_No_More()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """{"tickets":[{"id":1}],"meta":{"has_more":false,"after_cursor":"xxx"}}"""),
            "tickets", ZendeskDetail.Summary, DefaultBudget);

        Assert.False(envelope.GetProperty("has_more").GetBoolean());
        Assert.False(envelope.TryGetProperty("after_cursor", out _));
        Assert.False(envelope.TryGetProperty("next_page", out _));
    }

    [Fact]
    public void Offset_Envelope_Computes_Next_Page_As_A_Number_And_Never_Parses_Urls()
    {
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """
                {"results":[{"result_type":"ticket","id":1,"subject":"a"}],"count":205,
                 "next_page":"https://acme.zendesk.com/api/v2/search.json?page=4&query=q",
                 "previous_page":"https://acme.zendesk.com/api/v2/search.json?page=2&query=q","facets":null}
                """),
            "results", 3, ZendeskDetail.Summary, DefaultBudget);

        Assert.Equal(new[] { "detail", "count", "has_more", "next_page", "items" }, PropertyNames(envelope));
        Assert.Equal(205, envelope.GetProperty("count").GetInt64());
        Assert.True(envelope.GetProperty("has_more").GetBoolean());
        // (request page ?? 1) + 1 — computed, not parsed from the URL.
        Assert.Equal(4, envelope.GetProperty("next_page").GetInt32());
        Assert.False(envelope.TryGetProperty("after_cursor", out _));
    }

    [Fact]
    public void Offset_Envelope_Defaults_The_Request_Page_To_One()
    {
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """{"tickets":[{"id":1}],"count":50,"next_page":"https://x/tickets.json?page=2"}"""),
            "tickets", null, ZendeskDetail.Summary, DefaultBudget);

        Assert.Equal(2, envelope.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public void Offset_Envelope_Reports_No_More_When_Zendesk_Next_Page_Is_Null()
    {
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """{"tickets":[{"id":1}],"count":1,"next_page":null,"previous_page":null}"""),
            "tickets", null, ZendeskDetail.Summary, DefaultBudget);

        Assert.False(envelope.GetProperty("has_more").GetBoolean());
        Assert.False(envelope.TryGetProperty("next_page", out _));
    }

    [Fact]
    public void Search_Results_Dispatch_Per_Item_On_Result_Type()
    {
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """
                {"results":[
                  {"result_type":"ticket","id":1,"subject":"s","custom_fields":[{"id":9}],"description":"d"},
                  {"result_type":"user","id":2,"name":"Sam","email":"sam@example.org","user_fields":{"a":1}},
                  {"result_type":"organization","id":3,"name":"Acme","organization_fields":{"b":2}},
                  {"result_type":"group","id":4,"name":"Tier 1"}],
                 "count":4,"next_page":null}
                """),
            "results", null, ZendeskDetail.Summary, DefaultBudget);

        var items = envelope.GetProperty("items");
        Assert.Equal("ticket", items[0].GetProperty("result_type").GetString());
        Assert.False(items[0].TryGetProperty("custom_fields", out _));
        Assert.Equal("user", items[1].GetProperty("result_type").GetString());
        Assert.Equal("sam@example.org", items[1].GetProperty("email").GetString());
        Assert.False(items[1].TryGetProperty("user_fields", out _));
        Assert.Equal("organization", items[2].GetProperty("result_type").GetString());
        Assert.False(items[2].TryGetProperty("organization_fields", out _));
        Assert.Equal("group", items[3].GetProperty("result_type").GetString());
        Assert.Equal("Tier 1", items[3].GetProperty("name").GetString());
    }

    [Fact]
    public void Search_Result_With_An_Unmapped_Type_Fails_Visibly_As_Full_View_Plus_Note()
    {
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """
                {"results":[{"result_type":"topic","id":5,"title":"t","url":"https://api/topics/5.json",
                  "details":null}],"count":1,"next_page":null}
                """),
            "results", null, ZendeskDetail.Summary, DefaultBudget);

        var item = envelope.GetProperty("items")[0];
        Assert.Equal("t", item.GetProperty("title").GetString()); // full view, not silently dropped
        Assert.False(item.TryGetProperty("url", out _)); // ...but still full VIEW: self-link and nulls gone
        Assert.False(item.TryGetProperty("details", out _));
        Assert.Contains("result_type 'topic' has no summary shape", envelope.GetProperty("note").GetString());
    }

    [Fact]
    public void Sideloads_Keep_Their_Native_Names_And_Are_Summary_Projected()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """
                {"tickets":[{"id":1,"requester_id":2}],
                 "users":[{"id":2,"name":"Sam","photo":{"id":9},"user_fields":{"a":1}}],
                 "organizations":[{"id":3,"name":"Acme","organization_fields":{"b":1},"notes":"n"}],
                 "meta":{"has_more":false}}
                """),
            "tickets", ZendeskDetail.Summary, DefaultBudget);

        Assert.Equal(new[] { "detail", "has_more", "items", "users", "organizations" }, PropertyNames(envelope));
        var user = envelope.GetProperty("users")[0];
        Assert.Equal("Sam", user.GetProperty("name").GetString());
        Assert.False(user.TryGetProperty("photo", out _));
        Assert.False(envelope.GetProperty("organizations")[0].TryGetProperty("notes", out _));
    }

    [Fact]
    public void A_Sideload_Without_A_Summary_Shape_Is_Omitted_With_A_Note()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """
                {"tickets":[{"id":1}],"metric_sets":[{"id":7,"solved_at":"2026-07-01T00:00:00Z"}],
                 "meta":{"has_more":false}}
                """),
            "tickets", ZendeskDetail.Summary, DefaultBudget);

        Assert.False(envelope.TryGetProperty("metric_sets", out _));
        Assert.Equal("sideload metric_sets has no summary shape — use detail:'full'",
            envelope.GetProperty("note").GetString());
    }

    [Fact]
    public void Full_Detail_Keeps_Complete_Rows_And_Unknown_Sideloads_As_Full_View()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """
                {"tickets":[{"id":1,"subject":"s","url":"https://api/tickets/1.json","custom_fields":[{"id":9,"value":"x"}],
                  "assignee_id":null}],
                 "metric_sets":[{"id":7,"url":"https://api/metrics/7.json","solved_at":null,"replies":2}],
                 "meta":{"has_more":false}}
                """),
            "tickets", ZendeskDetail.Full, DefaultBudget);

        Assert.Equal("full", envelope.GetProperty("detail").GetString());
        var ticket = envelope.GetProperty("items")[0];
        Assert.True(ticket.TryGetProperty("custom_fields", out _)); // full record...
        Assert.False(ticket.TryGetProperty("url", out _)); // ...minus self-links
        Assert.False(ticket.TryGetProperty("assignee_id", out _)); // ...and nulls
        var metricSet = envelope.GetProperty("metric_sets")[0];
        Assert.Equal(2, metricSet.GetProperty("replies").GetInt32());
        Assert.False(metricSet.TryGetProperty("url", out _));
        Assert.False(envelope.TryGetProperty("note", out _)); // nothing was omitted — no note
    }

    [Fact]
    public void Explicitly_Requested_Sideload_Fields_Ride_On_Summary_Rows()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """{"tickets":[{"id":1,"subject":"s","comment_count":12}],"meta":{"has_more":false}}"""),
            "tickets", ZendeskDetail.Summary, DefaultBudget, extraSummaryFields: ["comment_count"]);

        Assert.Equal(12, envelope.GetProperty("items")[0].GetProperty("comment_count").GetInt32());
    }

    [Fact]
    public void The_Item_Shape_Can_Be_Overridden_When_The_Array_Name_Is_Not_The_Type()
    {
        // Help Center search returns article rows in a "results" array without result_type discriminators.
        var envelope = ZendeskLean.BuildOffsetListEnvelope(Parse(
                """
                {"results":[{"id":1,"title":"How to reset","html_url":"https://acme/hc/1","section_id":9,
                  "snippet":"How to <em>reset</em>","body":"<p>huge</p>"}],"count":1,"next_page":null}
                """),
            "results", null, ZendeskDetail.Summary, DefaultBudget, itemShapeName: "articles");

        var article = envelope.GetProperty("items")[0];
        Assert.Equal("How to reset", article.GetProperty("title").GetString());
        Assert.Equal("How to <em>reset</em>", article.GetProperty("snippet").GetString());
        Assert.False(article.TryGetProperty("body", out _));
    }

    [Fact]
    public void Primitive_Items_Pass_Through_Unchanged()
    {
        // tags_list rows are plain strings — no shape applies, and none is needed.
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """{"tags":["vip","enterprise"],"meta":{"has_more":false}}"""),
            "tags", ZendeskDetail.Summary, DefaultBudget);

        var items = envelope.GetProperty("items");
        Assert.Equal("vip", items[0].GetString());
        Assert.Equal("enterprise", items[1].GetString());
    }

    [Fact]
    public void A_Caller_Note_Is_Merged_Before_The_Envelope_Notes()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse(
                """{"tickets":[{"id":1}],"unknown_things":[{"id":2}],"meta":{"has_more":false}}"""),
            "tickets", ZendeskDetail.Summary, DefaultBudget, "3 inactive hidden — pass activeOnly:false");

        Assert.Equal(
            "3 inactive hidden — pass activeOnly:false; sideload unknown_things has no summary shape — use detail:'full'",
            envelope.GetProperty("note").GetString());
    }

    [Fact]
    public void An_Unregistered_Primary_Array_Of_Objects_Is_A_Programming_Error()
    {
        Assert.Throws<InvalidOperationException>(() => ZendeskLean.BuildCursorListEnvelope(
            Parse("""{"metric_sets":[{"id":1}],"meta":{"has_more":false}}"""),
            "metric_sets", ZendeskDetail.Summary, DefaultBudget));
    }

    [Fact]
    public void A_Missing_Items_Array_Produces_An_Empty_Envelope()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(Parse("""{"meta":{"has_more":false}}"""),
            "tickets", ZendeskDetail.Summary, DefaultBudget);

        Assert.Equal(0, envelope.GetProperty("items").GetArrayLength());
    }
}