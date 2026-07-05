using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The A1 sideload edge rule, pinned per tool: every sideload array name a tool's guidance offers must
///     either map to a registered <see cref="ZendeskLean" /> summary shape (so the sideload rows come back
///     summary-projected) or be EXPLICITLY classified here as omit-with-note (the visible-failure path:
///     "sideload X has no summary shape — use detail:'full'"). A sideload nobody classified — for example one
///     added to a tool description without a shape — fails this test instead of silently leaking or vanishing.
/// </summary>
public class ZendeskLeanSideloadContractTests
{
    /// <summary>
    ///     The sideload registry: every array name each ZendeskLean-enveloped tool's guidance offers via its
    ///     <c>include</c> parameter. (<c>tickets_comments_list</c> is absent by design: its <c>users</c>
    ///     sideload rides its bespoke typed projection, covered by the comments tests.) Values marked
    ///     <c>true</c> are expected to have a summary shape; <c>false</c> means deliberately omit-with-note.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Sideload, bool HasShape)[]> ToolSideloads =
        new Dictionary<string, (string, bool)[]>(StringComparer.Ordinal)
        {
            ["tickets_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["tickets_get_many"] = [("users", true), ("groups", true), ("organizations", true)],
            ["tickets_search"] = [("users", true), ("groups", true), ("organizations", true)],
            ["tickets_audits_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["tickets_export_incremental"] = [("users", true), ("groups", true), ("organizations", true)],
            ["users_list"] = [("organizations", true), ("groups", true), ("identities", true)],
            ["users_get_many"] = [("organizations", true), ("groups", true), ("identities", true)],
            ["users_tickets_requested_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["users_tickets_assigned_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["users_tickets_ccd_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["organizations_tickets_list"] = [("users", true), ("groups", true), ("organizations", true)],
            ["organizations_users_list"] = [("organizations", true), ("groups", true), ("identities", true)],
            // group_settings is an opaque, admin-shaped blob with no summary value — deliberately
            // omit-with-note; the tool description says it is only returned with detail:'full'.
            ["groups_list"] = [("users", true), ("group_settings", false)],
            ["groups_memberships_list"] = [("users", true), ("groups", true)],
            ["articles_list"] = [("users", true), ("sections", true), ("categories", true)]
        };

    public static TheoryData<string> RegisteredTools()
    {
        var data = new TheoryData<string>();
        foreach (var tool in ToolSideloads.Keys.Order(StringComparer.Ordinal)) data.Add(tool);
        return data;
    }

    [Theory]
    [MemberData(nameof(RegisteredTools))]
    public void Every_Known_Sideload_Is_Summary_Shaped_Or_Explicitly_Omit_With_Note(string tool)
    {
        foreach (var (sideload, hasShape) in ToolSideloads[tool])
            Assert.True(ZendeskLean.HasSummaryShape(sideload) == hasShape,
                $"{tool}: sideload '{sideload}' is classified {(hasShape ? "summary-shaped" : "omit-with-note")} " +
                "but ZendeskLean disagrees — register a shape or reclassify it explicitly.");
    }

    [Fact]
    public void An_Omit_With_Note_Sideload_Is_Dropped_From_Summary_And_The_Note_Names_The_Escalation()
    {
        // groups_list's group_settings: the only registered omit-with-note sideload — pin the visible failure.
        var response = JsonSerializer.SerializeToElement(JsonNode.Parse(
            """
            {"groups":[{"id":3,"name":"Tier 1"}],
             "group_settings":[{"id":1,"setting":"opaque admin blob"}]}
            """)!);

        var envelope = ZendeskLean.BuildOffsetListEnvelope(response, "groups", null,
            ZendeskDetail.Summary, 60_000);

        Assert.False(envelope.TryGetProperty("group_settings", out _));
        Assert.Equal("sideload group_settings has no summary shape — use detail:'full'",
            envelope.GetProperty("note").GetString());

        // ...and detail:'full' is a real escalation path: the sideload comes back.
        var full = ZendeskLean.BuildOffsetListEnvelope(response, "groups", null,
            ZendeskDetail.Full, 60_000);
        Assert.Equal(JsonValueKind.Array, full.GetProperty("group_settings").ValueKind);
    }

    [Fact]
    public void The_Row_Embedded_Translations_Sideload_Is_Stripped_From_Article_Summary_Rows()
    {
        // articles_list's fourth include value, translations, is NOT a sibling array — Zendesk embeds it in
        // each article row (body-heavy). The article allowlist strips it from summary rows; detail:'full' is
        // the documented escalation (with a size warning in the tool description).
        var article = (JsonObject)JsonNode.Parse(
            """
            {"id":401,"title":"How to reset","locale":"en-us",
             "translations":[{"locale":"da","body":"enormous translated body"}]}
            """)!;

        var row = ZendeskLean.SummarizeEntity("articles", article);

        Assert.NotNull(row);
        Assert.False(row.ContainsKey("translations"));
        Assert.False(ZendeskLean.HasSummaryShape("translations")); // and nobody registered it as a sibling shape
    }

    [Fact]
    public void The_Sideload_Materialized_Comment_Count_Rides_The_Row_Only_When_Requested()
    {
        // tickets_get_many's comment_count include is a row FIELD, not a sibling array: it is appended to the
        // ticket allowlist only for the call that requested it (extraSummaryFields), never by default.
        var ticket = (JsonObject)JsonNode.Parse("""{"id":101,"subject":"s","comment_count":7}""")!;

        var withoutSideload = ZendeskLean.SummarizeEntity("tickets", ticket);
        Assert.NotNull(withoutSideload);
        Assert.False(withoutSideload.ContainsKey("comment_count"));

        var withSideload = ZendeskLean.SummarizeEntity("tickets", ticket, ["comment_count"]);
        Assert.NotNull(withSideload);
        Assert.Equal(7, withSideload["comment_count"]!.GetValue<int>());
    }
}