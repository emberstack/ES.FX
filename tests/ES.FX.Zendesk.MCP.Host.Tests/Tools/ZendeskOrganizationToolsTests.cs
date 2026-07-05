using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskOrganizationToolsTests
{
    private static (ZendeskOrganizationTools Tools, ZendeskToolHarness Harness) Create(McpOptions? options = null)
    {
        var harness = new ZendeskToolHarness();
        return (new ZendeskOrganizationTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(options ?? new McpOptions())), harness);
    }

    [Fact]
    public async Task Read_Requests_The_Organization_And_Returns_The_Full_View()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization":{"id":7,"name":"Acme","url":"https://unit-test.zendesk.com/api/v2/organizations/7.json",
             "details":"Platinum tier","notes":"Renewal due Q3","organization_fields":{"plan":"platinum"},
             "group_id":null,"created_at":"2026-01-01T00:00:00Z"}}
            """);

        var result = await tools.Read(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7", request.Path);
        Assert.Equal(7, result.GetProperty("id").GetInt64());
        Assert.Equal("Acme", result.GetProperty("name").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", result.GetProperty("created_at").GetString());
        // organizations_get is the full-detail sink: notes/details and custom org fields survive ONLY here.
        Assert.Equal("Platinum tier", result.GetProperty("details").GetString());
        Assert.Equal("Renewal due Q3", result.GetProperty("notes").GetString());
        Assert.Equal("platinum", result.GetProperty("organization_fields").GetProperty("plan").GetString());
        // ...while the full view drops API self-links and null-valued fields (absent = null/empty).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("group_id", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Organization_Is_Missing()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(7, TestContext.Current.CancellationToken));

        Assert.Contains("'7'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Tickets_Sends_Paging_And_Include()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":1,"subject":"Down","description":"The portal is down again","status":"open",
             "url":"https://unit-test.zendesk.com/api/v2/tickets/1.json","custom_fields":[{"id":9,"value":"x"}]}],
             "count":5,"next_page":"https://unit-test.zendesk.com/api/v2/organizations/7/tickets.json?page=3",
             "users":[{"id":3,"name":"Agent","role":"agent","user_fields":{"vip":true}}]}
            """);

        var result = await tools.Tickets(7, 2, 25, ["users", "groups"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7/tickets", request.Path);
        Assert.Contains("page=2", request.Query);
        Assert.Contains("per_page=25", request.Query);
        Assert.Contains("include=users%2Cgroups", request.Query);
        // The lean envelope: metadata first, summary ticket rows, sideloads under their native names.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(5, result.GetProperty("count").GetInt32());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        // next_page is a computed page NUMBER ((request page 2) + 1) — Zendesk's URL string is never echoed.
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
        var ticket = result.GetProperty("items")[0];
        Assert.Equal(1, ticket.GetProperty("id").GetInt64());
        Assert.Equal("Down", ticket.GetProperty("subject").GetString());
        // Summary rows are allowlisted — the token-heavy members are stripped.
        Assert.False(ticket.TryGetProperty("custom_fields", out _));
        Assert.False(ticket.TryGetProperty("url", out _));
        // Sideloaded arrays survive under their native names, summary-projected.
        var agent = result.GetProperty("users")[0];
        Assert.Equal(3, agent.GetProperty("id").GetInt64());
        Assert.False(agent.TryGetProperty("user_fields", out _));
    }

    [Fact]
    public async Task Tickets_Omits_Unset_Parameters()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"tickets":[],"count":0}""");

        await tools.Tickets(7, null, null, null,
            TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, harness.Request.Query);
    }

    [Fact]
    public async Task Tickets_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"tickets":[],"count":0}""");

        await tools.Tickets(7, cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Tickets_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":1,"subject":"Down","url":"https://unit-test.zendesk.com/api/v2/tickets/1.json",
             "custom_fields":[{"id":9,"value":"x"}],"assignee_id":null}],"count":1,"next_page":null}
            """);

        var result = await tools.Tickets(7, cancellationToken: TestContext.Current.CancellationToken,
            detail: "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var ticket = result.GetProperty("items")[0];
        Assert.True(ticket.TryGetProperty("custom_fields", out _)); // the complete record...
        Assert.False(ticket.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(ticket.TryGetProperty("assignee_id", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task List_Uses_Cursor_Pagination()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organizations":[{"id":9,"name":"Acme","domain_names":["acme.com"],"notes":"internal note",
             "organization_fields":{"plan":"gold"},"url":"https://unit-test.zendesk.com/api/v2/organizations/9.json"}],
             "meta":{"has_more":false,"after_cursor":null}}
            """);

        var result = await tools.List(50, "cursor-1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations", request.Path);
        Assert.Contains("page%5Bsize%5D=50", request.Query);
        Assert.Contains("page%5Bafter%5D=cursor-1", request.Query);
        // The lean envelope: cursor continuation metadata at the top, allowlisted summary rows.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.False(result.TryGetProperty("after_cursor", out _));
        var organization = result.GetProperty("items")[0];
        Assert.Equal(9, organization.GetProperty("id").GetInt64());
        Assert.Equal("acme.com", organization.GetProperty("domain_names")[0].GetString());
        // notes/details and custom org fields are stripped from summary rows — organizations_get carries them.
        Assert.False(organization.TryGetProperty("notes", out _));
        Assert.False(organization.TryGetProperty("organization_fields", out _));
        Assert.False(organization.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[],"meta":{"has_more":false}}""");

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?page%5Bsize%5D=25", harness.Request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organizations":[{"id":9,"name":"Acme","notes":"internal note","organization_fields":{"plan":"gold"},
             "url":"https://unit-test.zendesk.com/api/v2/organizations/9.json","external_id":null}],
             "meta":{"has_more":false}}
            """);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var organization = result.GetProperty("items")[0];
        Assert.Equal("internal note", organization.GetProperty("notes").GetString()); // the complete record...
        Assert.True(organization.TryGetProperty("organization_fields", out _));
        Assert.False(organization.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(organization.TryGetProperty("external_id", out _)); // ...and null-valued fields
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
    public async Task Count_Requests_Organization_Count()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":321,"refreshed_at":"2026-07-01T00:00:00Z"}}""");

        var result = await tools.Count(TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/count", request.Path);
        var count = result.GetProperty("count");
        Assert.Equal(321, count.GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", count.GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task TicketsCount_Requests_The_Organization_Ticket_Count()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":1234,"refreshed_at":"2026-07-01T00:00:00Z"}}""");

        var result = await tools.TicketsCount(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7/tickets/count", request.Path);
        Assert.Equal(string.Empty, request.Query);
        var count = result.GetProperty("count");
        Assert.Equal(1234, count.GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", count.GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task UsersCount_Requests_The_Organization_User_Count()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"count":{"value":56,"refreshed_at":"2026-07-01T00:00:00Z"}}""");

        var result = await tools.UsersCount(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7/users/count", request.Path);
        Assert.Equal(string.Empty, request.Query);
        var count = result.GetProperty("count");
        Assert.Equal(56, count.GetProperty("value").GetInt64());
        Assert.Equal("2026-07-01T00:00:00Z", count.GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task ReadMany_Requests_Show_Many_With_Joined_Ids()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[{"id":7},{"id":8},{"id":9}],"count":3}""");

        var result = await tools.ReadMany([7, 8, 9], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/show_many", request.Path);
        Assert.Contains("ids=7%2C8%2C9", request.Query);
        Assert.Equal(3, result.GetProperty("items").GetArrayLength());
        Assert.Equal(3, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ReadMany_Returns_Empty_Result_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create();

        var result = await tools.ReadMany([], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ReadMany_Rejects_More_Than_100_Ids_With_A_Batching_Instruction()
    {
        // show_many rejects >100 ids with a 400 — the tool surfaces the contract as an actionable batching
        // error instead of fanning out server-side (the agent controls—and pays for—each call).
        var (tools, harness) = Create();
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ReadMany(ids, TestContext.Current.CancellationToken));

        Assert.Contains("100", exception.Message);
        Assert.Contains("101", exception.Message);
        Assert.Contains("batch", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Search_Sends_Name()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[{"id":1,"name":"Acme","notes":"internal note"}],"count":1}""");

        var result = await tools.Search("Acme", null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/organizations/search", request.Path);
        Assert.Contains("name=Acme", request.Query);
        Assert.DoesNotContain("external_id", request.Query);
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        var organization = result.GetProperty("items")[0];
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        // Summary rows are allowlisted — notes/details stay behind organizations_get or detail:'full'.
        Assert.False(organization.TryGetProperty("notes", out _));
    }

    [Fact]
    public async Task Search_Sends_ExternalId()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[{"id":1,"external_id":"ext-42"}],"count":1}""");

        var result = await tools.Search(null, "ext-42", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/organizations/search", request.Path);
        Assert.Contains("external_id=ext-42", request.Query);
        Assert.DoesNotContain("name=", request.Query);
        // external_id is part of the organization summary shape — the lookup key stays visible on the rows.
        Assert.Equal("ext-42", result.GetProperty("items")[0].GetProperty("external_id").GetString());
    }

    [Theory]
    [InlineData("Acme", "ext-42")]
    [InlineData(null, null)]
    [InlineData(" ", "")]
    public async Task Search_Rejects_Both_Or_Neither(string? name, string? externalId)
    {
        var (tools, harness) = Create();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Search(name, externalId, TestContext.Current.CancellationToken));

        Assert.StartsWith("Provide exactly one of name or externalId.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Autocomplete_Sends_Name_And_Offset_Paging()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[{"id":2,"name":"Acme"}],"count":2,"next_page":null}""");

        var result = await tools.Autocomplete("Acm", 1, 20, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/organizations/autocomplete", request.Path);
        Assert.Contains("name=Acm", request.Query);
        Assert.Contains("&page=1", request.Query);
        Assert.Contains("per_page=20", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(2, result.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Autocomplete_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organizations":[],"count":0}""");

        await tools.Autocomplete("Acm", cancellationToken: TestContext.Current.CancellationToken);

        // The autocomplete endpoint is throttled, so the default page is 10 — explicit on the wire.
        Assert.Equal("?name=Acm&per_page=10", harness.Request.Query);
    }

    [Fact]
    public async Task Autocomplete_Rejects_Blank_Name()
    {
        var (tools, harness) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Autocomplete(" ", null, null, TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Users_Sends_Paging_And_Include()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"users":[{"id":3,"name":"Ann","email":"ann@example.com","role":"end-user",
             "url":"https://unit-test.zendesk.com/api/v2/users/3.json","user_fields":{"plan":"gold"}}],"count":4,
             "identities":[{"id":11,"user_id":3,"type":"email","value":"ann@example.com","primary":true,
             "verified":true,"url":"https://unit-test.zendesk.com/api/v2/users/3/identities/11.json"}]}
            """);

        var result = await tools.Users(7, 1, 50, ["identities"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7/users", request.Path);
        Assert.Contains("page=1", request.Query);
        Assert.Contains("per_page=50", request.Query);
        Assert.Contains("include=identities", request.Query);
        Assert.Equal(4, result.GetProperty("count").GetInt32());
        var user = result.GetProperty("items")[0];
        Assert.Equal(3, user.GetProperty("id").GetInt64());
        Assert.Equal("ann@example.com", user.GetProperty("email").GetString());
        // Summary rows are allowlisted — custom user fields and API self-links do not appear.
        Assert.False(user.TryGetProperty("user_fields", out _));
        Assert.False(user.TryGetProperty("url", out _));
        // Sideloaded arrays survive under their native names, summary-projected.
        var identity = result.GetProperty("identities")[0];
        Assert.Equal(11, identity.GetProperty("id").GetInt64());
        Assert.Equal("email", identity.GetProperty("type").GetString());
        Assert.False(identity.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Users_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"users":[],"count":0}""");

        await tools.Users(7, cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Users_Detail_Full_Returns_Unstripped_Rows()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"users":[{"id":3,"name":"Ann","user_fields":{"plan":"gold"},
             "url":"https://unit-test.zendesk.com/api/v2/users/3.json","phone":null}],"count":1}
            """);

        var result = await tools.Users(7, cancellationToken: TestContext.Current.CancellationToken,
            detail: "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var user = result.GetProperty("items")[0];
        Assert.True(user.TryGetProperty("user_fields", out _)); // the complete record...
        Assert.False(user.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(user.TryGetProperty("phone", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task Memberships_Sends_Paging()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization_memberships":[{"id":77,"user_id":11,"organization_id":22,"default":true,
             "url":"https://unit-test.zendesk.com/api/v2/organization_memberships/77.json",
             "organization_name":null}],"count":5}
            """);

        var result = await tools.Memberships(7, 2, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/organizations/7/organization_memberships", request.Path);
        Assert.Contains("page=2", request.Query);
        Assert.Contains("per_page=100", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(5, result.GetProperty("count").GetInt32());
        var membership = result.GetProperty("items")[0];
        // The membership id (needed to delete a link) and the default flag stay on the rows...
        Assert.Equal(77, membership.GetProperty("id").GetInt64());
        Assert.Equal(11, membership.GetProperty("user_id").GetInt64());
        Assert.True(membership.GetProperty("default").GetBoolean());
        // ...while API self-links and null-valued fields are dropped (the lean form of these tiny rows).
        Assert.False(membership.TryGetProperty("url", out _));
        Assert.False(membership.TryGetProperty("organization_name", out _));
    }

    [Fact]
    public async Task Memberships_Applies_The_Default_Page_Size()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organization_memberships":[],"count":0}""");

        await tools.Memberships(7, cancellationToken: TestContext.Current.CancellationToken);

        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task Memberships_Detail_Full_Returns_The_Same_Full_View_Rows()
    {
        // Memberships have no summary shape (nothing to strip beyond the full view), so both detail levels
        // return the same rows — only the envelope's detail echo differs.
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization_memberships":[{"id":77,"user_id":11,"default":false,
             "url":"https://unit-test.zendesk.com/api/v2/organization_memberships/77.json"}],"count":1}
            """);

        var result = await tools.Memberships(7, cancellationToken: TestContext.Current.CancellationToken,
            detail: "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var membership = result.GetProperty("items")[0];
        Assert.Equal(77, membership.GetProperty("id").GetInt64());
        Assert.False(membership.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task MergeStatus_Requests_Merge_By_String_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """{"organization_merge":{"id":"merge-1","winner_id":9,"loser_id":5,"status":"complete"}}""");

        var result = await tools.MergeStatus("merge-1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organization_merges/merge-1", request.Path);
        var merge = result.GetProperty("organization_merge");
        Assert.Equal("merge-1", merge.GetProperty("id").GetString());
        Assert.Equal("complete", merge.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MergeStatus_Rejects_Blank_Id()
    {
        var (tools, harness) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.MergeStatus(" ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Tags_Requests_Tags()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"tags":["vip"],"count":1}""");

        var result = await tools.Tags(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/organizations/7/tags", request.Path);
        Assert.Equal("vip", result.GetProperty("tags")[0].GetString());
    }
}