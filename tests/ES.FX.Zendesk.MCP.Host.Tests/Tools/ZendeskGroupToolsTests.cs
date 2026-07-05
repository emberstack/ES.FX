using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskGroupToolsTests
{
    private static (ZendeskGroupTools Tools, ZendeskToolHarness Harness) Create()
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskGroupTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions())), harness);
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"groups":[{"url":"https://unit-test.zendesk.com/api/v2/groups/55.json","id":55,"name":"Tier 2",
            "default":false,"created_at":"2026-01-02T03:04:05Z","updated_at":"2026-02-03T04:05:06Z"}],
            "count":3,"next_page":null,"previous_page":null}
            """);

        var result = await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups", request.Path);
        // The default page size is explicit on the wire — 25, never left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", request.Query);
        // The lean envelope: metadata first, allowlisted group summary rows last.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(3, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.False(result.TryGetProperty("next_page", out _));
        var group = result.GetProperty("items")[0];
        Assert.Equal(55, group.GetProperty("id").GetInt64());
        Assert.Equal("Tier 2", group.GetProperty("name").GetString());
        Assert.False(group.GetProperty("default").GetBoolean());
        // Summary rows are allowlisted — API self-links and timestamps do not appear.
        Assert.False(group.TryGetProperty("url", out _));
        Assert.False(group.TryGetProperty("created_at", out _));
    }

    [Fact]
    public async Task List_Passes_Page_And_Include_Through_And_Summary_Projects_Sideloads()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"groups":[{"id":55,"name":"Tier 2"}],
            "users":[{"id":11,"name":"Agent","url":"https://unit-test.zendesk.com/api/v2/users/11.json"}],
            "group_settings":[{"group_id":55,"some_setting":true}],
            "count":1,"next_page":"https://unit-test.zendesk.com/api/v2/groups.json?page=3"}
            """);

        var result = await tools.List(2, 100, ["users", "group_settings"],
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/groups", request.Path);
        Assert.Contains("page=2", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Contains("include=users%2Cgroup_settings", request.Query);
        Assert.Equal(55, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        // Offset continuation is a computed page NUMBER — Zendesk's URL string is never echoed.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
        // The users sideload survives under its native name, summary-projected (no API self-links).
        var user = result.GetProperty("users")[0];
        Assert.Equal(11, user.GetProperty("id").GetInt64());
        Assert.False(user.TryGetProperty("url", out _));
        // group_settings has no summary shape — omitted visibly, with the escalation path in the note.
        Assert.False(result.TryGetProperty("group_settings", out _));
        Assert.Contains("group_settings", result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task List_Omits_Empty_Include()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"groups":[]}""");

        await tools.List(null, 100, [], TestContext.Current.CancellationToken);

        Assert.DoesNotContain("include", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"groups":[{"id":55,"name":"Tier 2","url":"https://unit-test.zendesk.com/api/v2/groups/55.json",
            "created_at":"2026-01-02T03:04:05Z","deleted":null}],"count":1,"next_page":null}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var group = result.GetProperty("items")[0];
        Assert.True(group.TryGetProperty("created_at", out _)); // the complete record...
        Assert.False(group.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(group.TryGetProperty("deleted", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Requests_The_Group_And_Returns_The_Full_View()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group":{"id":55,"url":"https://unit-test.zendesk.com/api/v2/groups/55.json","name":"Tier 2",
            "description":null,"created_at":"2026-01-02T03:04:05Z","deleted":false}}
            """);

        var result = await tools.Read(55, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/55", request.Path);
        // The full-detail sink: the unwrapped group keeps every wire field the summary rows strip...
        Assert.Equal("Tier 2", result.GetProperty("name").GetString());
        Assert.Equal(55, result.GetProperty("id").GetInt64());
        Assert.Equal("2026-01-02T03:04:05Z", result.GetProperty("created_at").GetString());
        Assert.False(result.GetProperty("deleted").GetBoolean());
        // ...minus API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Group_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(55, TestContext.Current.CancellationToken));

        Assert.Contains("'55'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Memberships_Requests_Paging_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group_memberships":[{"id":88,"url":"https://unit-test.zendesk.com/api/v2/group_memberships/88.json",
            "user_id":11,"group_id":55,"default":true,"created_at":"2026-01-02T03:04:05Z",
            "future_field":"stripped"}],"count":4,"next_page":null}
            """);

        var result = await tools.Memberships(55, null, 100, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/55/memberships", request.Path);
        Assert.Equal("?per_page=100", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(4, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        // The summary row keeps the complete routing identity...
        var membership = result.GetProperty("items")[0];
        Assert.Equal(88, membership.GetProperty("id").GetInt64());
        Assert.Equal(11, membership.GetProperty("user_id").GetInt64());
        Assert.Equal(55, membership.GetProperty("group_id").GetInt64());
        Assert.True(membership.GetProperty("default").GetBoolean());
        Assert.Equal("2026-01-02T03:04:05Z", membership.GetProperty("created_at").GetString());
        // ...and is allowlisted: API self-links and unknown wire fields do not appear.
        Assert.False(membership.TryGetProperty("url", out _));
        Assert.False(membership.TryGetProperty("future_field", out _));
    }

    [Fact]
    public async Task Memberships_Passes_Include_Through_And_Summary_Projects_The_Users_Sideload()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group_memberships":[{"id":88,"user_id":11,"group_id":55}],
            "users":[{"id":11,"name":"Agent","url":"https://unit-test.zendesk.com/api/v2/users/11.json"}],"count":2}
            """);

        var result = await tools.Memberships(55, null, 100, ["users"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/groups/55/memberships", request.Path);
        Assert.Contains("include=users", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Equal(88, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        // The sideload survives under its native name, summary-projected (allowlist rows, no API self-links).
        var user = result.GetProperty("users")[0];
        Assert.Equal(11, user.GetProperty("id").GetInt64());
        Assert.Equal("Agent", user.GetProperty("name").GetString());
        Assert.False(user.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Memberships_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"group_memberships":[],"count":0}""");

        await tools.Memberships(55, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Memberships_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group_memberships":[{"id":88,"user_id":11,"group_id":55,"default":false,
            "url":"https://unit-test.zendesk.com/api/v2/group_memberships/88.json","future_field":"kept"}],
            "count":1,"next_page":null}
            """);

        var result = await tools.Memberships(55, null, 25, null, TestContext.Current.CancellationToken, "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var membership = result.GetProperty("items")[0];
        // Full rows keep everything the summary allowlist would strip...
        Assert.Equal("kept", membership.GetProperty("future_field").GetString());
        // ...but are still the full VIEW: API self-links are gone.
        Assert.False(membership.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Memberships_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Memberships(55, detail: "raw", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Assignable_Requests_Paging_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """{"groups":[{"id":55,"name":"Tier 2","created_at":"2026-01-02T03:04:05Z"}],"count":2}""");

        var result = await tools.Assignable(2, 50, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/assignable", request.Path);
        Assert.Contains("page=2", request.Query);
        Assert.Contains("per_page=50", request.Query);
        // The group id is the whole point of this lookup tool — it must survive on the summary rows.
        var group = result.GetProperty("items")[0];
        Assert.Equal(55, group.GetProperty("id").GetInt64());
        Assert.Equal("Tier 2", group.GetProperty("name").GetString());
        // Summary rows are allowlisted — timestamps do not appear.
        Assert.False(group.TryGetProperty("created_at", out _));
        Assert.Equal(2, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Assignable_Applies_The_Default_Page_Size()
    {
        // Regression: the guidance used to claim a default of 100 while the tool sent no per_page at all —
        // the default is now 25 and explicit on the wire.
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"groups":[],"count":0}""");

        await tools.Assignable(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Count_Delegates()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":12,"refreshed_at":"2026-07-01T00:00:00Z"}}""");

        var result = await tools.Count(TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/count", request.Path);
        // The whole count object is read-only in the spec — both fields must survive the passthrough.
        Assert.Equal(12, result.GetProperty("count").GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", result.GetProperty("count").GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task Users_Requests_Paging_And_Returns_Summary_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"users":[{"id":11,"name":"Agent","role":"agent","email":"agent@example.com",
            "url":"https://unit-test.zendesk.com/api/v2/users/11.json","created_at":"2026-01-02T03:04:05Z"}],
            "count":6,"next_page":null}
            """);

        var result = await tools.Users(55, 1, 30, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/55/users", request.Path);
        Assert.Contains("page=1", request.Query);
        Assert.Contains("per_page=30", request.Query);
        // Rows are lean user summaries: triage fields stay, timestamps and API self-links are stripped.
        var user = result.GetProperty("items")[0];
        Assert.Equal(11, user.GetProperty("id").GetInt64());
        Assert.Equal("agent", user.GetProperty("role").GetString());
        Assert.Equal("agent@example.com", user.GetProperty("email").GetString());
        Assert.False(user.TryGetProperty("created_at", out _));
        Assert.False(user.TryGetProperty("url", out _));
        Assert.Equal(6, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Users_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"users":[{"id":11,"name":"Agent","url":"https://unit-test.zendesk.com/api/v2/users/11.json",
            "created_at":"2026-01-02T03:04:05Z","organization_id":null}],"count":1,"next_page":null}
            """);

        var result = await tools.Users(55, null, 25, TestContext.Current.CancellationToken, "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var user = result.GetProperty("items")[0];
        Assert.True(user.TryGetProperty("created_at", out _)); // the complete record...
        Assert.False(user.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(user.TryGetProperty("organization_id", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task Users_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"users":[],"count":0}""");

        await tools.Users(55, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task UsersCount_Delegates()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":42,"refreshed_at":"2026-07-01T00:00:00Z"}}""");

        var result = await tools.UsersCount(55, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/groups/55/users/count", request.Path);
        // Mirrors groups_count: the whole count object (value + refreshed_at freshness) passes through.
        Assert.Equal(42, result.GetProperty("count").GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", result.GetProperty("count").GetProperty("refreshed_at").GetString());
    }
}