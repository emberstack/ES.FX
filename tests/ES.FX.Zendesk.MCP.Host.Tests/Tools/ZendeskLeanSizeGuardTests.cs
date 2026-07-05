using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The response-size guard: list envelopes over budget drop tail items and suppress the continuation token
///     (never both a cursor and a truncation note); non-list responses over budget fail with an actionable
///     error naming the calling tool's own escalation parameters.
/// </summary>
public class ZendeskLeanSizeGuardTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>A cursor list of <paramref name="count" /> tickets whose subjects are ~200 chars each.</summary>
    private static JsonElement BigCursorList(int count, bool hasMore = true)
    {
        var tickets = new JsonArray();
        for (var i = 0; i < count; i++)
            tickets.Add(new JsonObject
            {
                ["id"] = i + 1,
                ["subject"] = $"ticket {i + 1} " + new string('s', 200),
                ["status"] = "open"
            });
        var response = new JsonObject
        {
            ["tickets"] = tickets,
            ["meta"] = new JsonObject { ["has_more"] = hasMore, ["after_cursor"] = "cursor-token" }
        };
        return JsonSerializer.SerializeToElement(response);
    }

    [Fact]
    public void List_Truncation_Drops_Tail_Items_Suppresses_The_Cursor_And_Explains_The_Recovery()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(BigCursorList(10), "tickets",
            ZendeskDetail.Summary, 1500);

        var items = envelope.GetProperty("items");
        var kept = items.GetArrayLength();
        Assert.InRange(kept, 1, 9); // tail items dropped, head items kept
        Assert.Equal(1, items[0].GetProperty("id").GetInt64()); // the HEAD survives — order intact

        // The continuation token is suppressed and has_more forced true: resuming from the cursor would
        // silently skip the dropped items.
        Assert.True(envelope.GetProperty("has_more").GetBoolean());
        Assert.False(envelope.TryGetProperty("after_cursor", out _));
        Assert.False(envelope.TryGetProperty("next_page", out _));

        var note = envelope.GetProperty("note").GetString()!;
        Assert.Contains($"items {kept + 1}–10 of this page were dropped", note);
        Assert.Contains($"pageSize:{kept}", note); // cursor regime → pageSize, not perPage

        // The final response actually fits the budget.
        Assert.True(envelope.GetRawText().Length <= 1500);
    }

    [Fact]
    public void List_Truncation_Never_Emits_A_Continuation_Token_With_A_Truncation_Note()
    {
        // The invariant, stated by the design: a truncation note and a continuation token are mutually
        // exclusive — across a range of budgets tight enough to truncate.
        foreach (var budget in new[] { 700, 1000, 1500, 2200 })
        {
            var envelope = ZendeskLean.BuildCursorListEnvelope(BigCursorList(10), "tickets",
                ZendeskDetail.Summary, budget);

            var truncated = envelope.TryGetProperty("note", out var note) &&
                            note.GetString()!.Contains("dropped");
            var hasContinuation = envelope.TryGetProperty("after_cursor", out _) ||
                                  envelope.TryGetProperty("next_page", out _);
            Assert.True(truncated, $"budget {budget} was expected to truncate");
            Assert.False(hasContinuation, $"budget {budget} leaked a continuation token past a truncation");
        }
    }

    [Fact]
    public void Offset_List_Truncation_Suppresses_The_Computed_Next_Page()
    {
        var tickets = new JsonArray();
        for (var i = 0; i < 6; i++)
            tickets.Add(new JsonObject { ["id"] = i + 1, ["subject"] = new string('s', 300) });
        var response = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["tickets"] = tickets,
            ["count"] = 600,
            ["next_page"] = "https://acme.zendesk.com/api/v2/tickets.json?page=2"
        });

        var envelope = ZendeskLean.BuildOffsetListEnvelope(response, "tickets", 1,
            ZendeskDetail.Summary, 1200);

        Assert.True(envelope.GetProperty("has_more").GetBoolean());
        Assert.False(envelope.TryGetProperty("next_page", out _));
        var note = envelope.GetProperty("note").GetString()!;
        Assert.Contains("dropped", note);
        var keptItems = envelope.GetProperty("items").GetArrayLength();
        Assert.Contains($"perPage:{keptItems}", note); // offset regime → perPage, not pageSize
    }

    [Fact]
    public void A_Single_Oversized_Item_Leaves_An_Empty_Page_With_The_Recovery_Note()
    {
        var response = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["tickets"] = new JsonArray(new JsonObject { ["id"] = 1, ["subject"] = new string('s', 5000) }),
            ["meta"] = new JsonObject { ["has_more"] = false }
        });

        var envelope = ZendeskLean.BuildCursorListEnvelope(response, "tickets", ZendeskDetail.Summary,
            1000);

        Assert.Equal(0, envelope.GetProperty("items").GetArrayLength());
        Assert.True(envelope.GetProperty("has_more").GetBoolean()); // the dropped item IS more
        var note = envelope.GetProperty("note").GetString()!;
        Assert.Contains("all 1 items of this page were dropped", note);
        Assert.True(envelope.GetRawText().Length <= 1000);
    }

    [Fact]
    public void Oversized_Sideloads_Are_Dropped_As_A_Last_Resort()
    {
        // Full detail with a huge unknown sideload: after every item is gone the sideload must go too.
        var sideload = new JsonArray();
        for (var i = 0; i < 5; i++) sideload.Add(new JsonObject { ["id"] = i, ["blob"] = new string('b', 800) });
        var response = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["tickets"] = new JsonArray(new JsonObject { ["id"] = 1, ["subject"] = "s" }),
            ["metric_sets"] = sideload,
            ["meta"] = new JsonObject { ["has_more"] = false }
        });

        var envelope = ZendeskLean.BuildCursorListEnvelope(response, "tickets", ZendeskDetail.Full,
            1000);

        Assert.False(envelope.TryGetProperty("metric_sets", out _));
        Assert.Contains("sideloaded arrays were also dropped", envelope.GetProperty("note").GetString());
        Assert.True(envelope.GetRawText().Length <= 1000);
    }

    [Fact]
    public void A_Fitting_Envelope_Is_Left_Alone()
    {
        var envelope = ZendeskLean.BuildCursorListEnvelope(BigCursorList(3), "tickets",
            ZendeskDetail.Summary, 60_000);

        Assert.Equal(3, envelope.GetProperty("items").GetArrayLength());
        Assert.Equal("cursor-token", envelope.GetProperty("after_cursor").GetString());
        Assert.False(envelope.TryGetProperty("note", out _));
    }

    [Fact]
    public void EnsureWithinBudget_Returns_A_Fitting_Response_Unchanged()
    {
        var response = Parse("""{"ticket":{"id":1}}""");

        var guarded = ZendeskLean.EnsureWithinBudget(response, "tickets_get", 60_000,
            "Escalation hint — unused here.");

        Assert.Equal(response.GetRawText(), guarded.GetRawText());
    }

    [Fact]
    public void EnsureWithinBudget_Throws_An_Actionable_Error_Naming_The_Tool_And_Its_Escalation()
    {
        var response = JsonSerializer.SerializeToElement(new JsonObject { ["body"] = new string('x', 3000) });

        var exception = Assert.Throws<McpException>(() => ZendeskLean.EnsureWithinBudget(response,
            "articles_get", 1000, "Re-call with maxBodyChars:2000 or bodyFormat:'plain'."));

        Assert.Contains("articles_get", exception.Message);
        Assert.Contains("1000-character", exception.Message);
        Assert.Contains("Re-call with maxBodyChars:2000 or bodyFormat:'plain'.", exception.Message);
    }
}