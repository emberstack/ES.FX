using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskUserToolsTests
{
    private static ZendeskUserTools CreateTools(ZendeskToolHarness harness) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(),
            new StaticOptionsMonitor<McpOptions>(new McpOptions()));

    /// <summary>Returns the decoded value of a query parameter by its raw (still-encoded) name, or null.</summary>
    private static string? QueryValue(ZendeskToolHarness.CapturedRequest request, string encodedName) =>
        request.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts[0] == encodedName)
            .Select(parts => Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : string.Empty))
            .SingleOrDefault();

    [Fact]
    public async Task Whoami_Requests_The_Current_User_And_Returns_The_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"user":{"id":7,"name":"Agent Smith","url":"https://unit-test.zendesk.com/api/v2/users/7.json",
             "active":true,"organization_id":null,"created_at":"2024-01-01T00:00:00Z"}}
            """);
        var tools = CreateTools(harness);

        var user = await tools.Whoami(TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/users/me", request.Path);
        // The full-view sink keeps the server-assigned (spec read-only) fields the generated models drop...
        Assert.Equal(7, user.GetProperty("id").GetInt64());
        Assert.Equal("Agent Smith", user.GetProperty("name").GetString());
        Assert.True(user.GetProperty("active").GetBoolean());
        Assert.Equal("2024-01-01T00:00:00Z", user.GetProperty("created_at").GetString());
        // ...while dropping API self-links and null-valued fields (absent = null/empty).
        Assert.False(user.TryGetProperty("url", out _));
        Assert.False(user.TryGetProperty("organization_id", out _));
    }

    [Fact]
    public async Task Whoami_Throws_When_Zendesk_Returns_No_User()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Whoami(TestContext.Current.CancellationToken));

        Assert.Contains("no user", exception.Message);
    }

    [Fact]
    public async Task Read_Requests_The_User_And_Returns_The_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"user":{"id":42,"name":"Jane","url":"https://unit-test.zendesk.com/api/v2/users/42.json",
             "created_at":"2024-02-02T00:00:00Z","updated_at":"2024-03-03T00:00:00Z","details":null,
             "user_fields":{"tier":"gold"},
             "photo":{"id":9,"url":"https://unit-test.zendesk.com/api/v2/attachments/9.json",
              "content_url":"https://unit-test.zendesk.com/attachments/photo.png"}}}
            """);
        var tools = CreateTools(harness);

        var user = await tools.Read(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/users/42", request.Path);
        Assert.Equal(42, user.GetProperty("id").GetInt64());
        // Server-assigned timestamps must survive the raw passthrough.
        Assert.Equal("2024-02-02T00:00:00Z", user.GetProperty("created_at").GetString());
        Assert.Equal("2024-03-03T00:00:00Z", user.GetProperty("updated_at").GetString());
        // users_get is the detail sink: custom user_fields and the photo survive here — only API self-links
        // and null-valued fields are stripped (content_url is a different name and is kept).
        Assert.Equal("gold", user.GetProperty("user_fields").GetProperty("tier").GetString());
        Assert.False(user.GetProperty("photo").TryGetProperty("url", out _));
        Assert.Equal("https://unit-test.zendesk.com/attachments/photo.png",
            user.GetProperty("photo").GetProperty("content_url").GetString());
        Assert.False(user.TryGetProperty("url", out _));
        Assert.False(user.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_User_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(42, TestContext.Current.CancellationToken));

        Assert.Contains("'42'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Search_Passes_Parameters_Through_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"users":[{"id":1,"name":"Jane","email":"jane@example.com","role":"agent",
             "created_at":"2024-01-01T00:00:00Z","photo":{"id":9}}],"count":3,
             "next_page":"https://unit-test.zendesk.com/api/v2/users/search.json?page=3"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Search("role:agent", 2, 50, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/search", request.Path);
        Assert.Equal("role:agent", QueryValue(request, "query"));
        Assert.Equal("2", QueryValue(request, "page"));
        Assert.Equal("50", QueryValue(request, "per_page"));
        // The lean envelope: summary rows, count, and next_page as a computed page NUMBER (never a URL).
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var user = result.GetProperty("items")[0];
        Assert.Equal(1, user.GetProperty("id").GetInt64());
        Assert.Equal("jane@example.com", user.GetProperty("email").GetString());
        // Summary rows are allowlisted — fields outside the user shape (photo, created_at) do not appear.
        Assert.False(user.TryGetProperty("photo", out _));
        Assert.False(user.TryGetProperty("created_at", out _));
        Assert.Equal(3, result.GetProperty("count").GetInt64());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(3, result.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public async Task ReadMany_Delegates_To_ShowMany()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"users":[{"id":1,"created_at":"2024-01-01T00:00:00Z"},{"id":2}],"count":2}""");
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([1, 2], cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/show_many", request.Path);
        Assert.Equal("1,2", QueryValue(request, "ids"));
        Assert.Null(QueryValue(request, "include"));
        var items = result.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(2, items[1].GetProperty("id").GetInt64());
        // created_at is outside the user summary shape — the rows stay lean.
        Assert.False(items[0].TryGetProperty("created_at", out _));
        Assert.Equal(2, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task ReadMany_Passes_Include_Through_And_Summarizes_The_Sideload()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"users":[{"id":1}],"count":1,"organizations":[{"id":9,"name":"Acme",
             "url":"https://unit-test.zendesk.com/api/v2/organizations/9.json"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([1], ["organizations"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/show_many", request.Path);
        Assert.Equal("organizations", QueryValue(request, "include"));
        Assert.Equal(1, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        // Sideloaded arrays survive under their native names, summary-projected (no API self-links).
        var organization = result.GetProperty("organizations")[0];
        Assert.Equal(9, organization.GetProperty("id").GetInt64());
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        Assert.False(organization.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task ReadMany_Prunes_The_Identities_Sideload_To_The_Identity_Shape()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"users":[{"id":1}],"count":1,
             "identities":[{"id":301,"user_id":1,"type":"email","value":"jane@example.com","primary":true,
              "verified":true,"url":"https://unit-test.zendesk.com/api/v2/users/1/identities/301.json",
              "created_at":"2024-01-01T00:00:00Z","deliverable_state":"deliverable"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([1], ["identities"], TestContext.Current.CancellationToken);

        Assert.Equal("identities", QueryValue(harness.Request, "include"));
        var identity = result.GetProperty("identities")[0];
        Assert.Equal(301, identity.GetProperty("id").GetInt64());
        Assert.Equal(1, identity.GetProperty("user_id").GetInt64());
        Assert.Equal("email", identity.GetProperty("type").GetString());
        Assert.Equal("jane@example.com", identity.GetProperty("value").GetString());
        Assert.True(identity.GetProperty("primary").GetBoolean());
        Assert.True(identity.GetProperty("verified").GetBoolean());
        // The identity summary shape prunes everything else (self-link, timestamps, delivery state).
        Assert.False(identity.TryGetProperty("url", out _));
        Assert.False(identity.TryGetProperty("created_at", out _));
        Assert.False(identity.TryGetProperty("deliverable_state", out _));
    }

    [Fact]
    public async Task ReadMany_Returns_Empty_Result_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        Assert.Equal(0, result.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ReadMany_Rejects_More_Than_100_Ids_With_A_Batching_Instruction()
    {
        // show_many rejects >100 ids with a 400 — the tool surfaces the contract as an actionable batching
        // error instead of fanning out server-side (the agent controls—and pays for—each call).
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.ReadMany(ids, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("100", exception.Message);
        Assert.Contains("101", exception.Message);
        Assert.Contains("batch", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task RequestedTickets_Delegates_And_Returns_Ticket_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":11,"subject":"Printer","status":"open","created_at":"2024-01-01T00:00:00Z",
             "custom_fields":[{"id":1,"value":"x"}]}],"count":4,
             "next_page":"https://unit-test.zendesk.com/api/v2/users/42/tickets/requested.json?page=2"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.RequestedTickets(42, null, 25, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/tickets/requested", request.Path);
        Assert.Equal("25", QueryValue(request, "per_page"));
        Assert.Null(QueryValue(request, "page"));
        Assert.Null(QueryValue(request, "include"));
        // Ticket rows use the shared ticket summary shape — token-heavy members do not appear.
        var ticket = result.GetProperty("items")[0];
        Assert.Equal(11, ticket.GetProperty("id").GetInt64());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.Equal("2024-01-01T00:00:00Z", ticket.GetProperty("created_at").GetString());
        Assert.False(ticket.TryGetProperty("custom_fields", out _));
        Assert.Equal(4, result.GetProperty("count").GetInt64());
        // Zendesk's next_page URL becomes a computed page NUMBER ((page ?? 1) + 1) — never an echoed URL.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(2, result.GetProperty("next_page").GetInt32());
    }

    [Fact]
    public async Task RequestedTickets_Passes_Page_And_Include_Through()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.RequestedTickets(42, 2, 50, ["users", "groups"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/tickets/requested", request.Path);
        Assert.Equal("2", QueryValue(request, "page"));
        Assert.Equal("50", QueryValue(request, "per_page"));
        Assert.Equal("users,groups", QueryValue(request, "include"));
    }

    [Fact]
    public async Task List_Delegates_With_Role_And_Cursor()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"users":[{"id":9,"name":"Neo","created_at":"2024-01-01T00:00:00Z"}],
             "groups":[{"id":31,"name":"Level 2"}],
             "meta":{"has_more":true,"after_cursor":"cursor-2"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List("agent", 50, "cursor-1", ["groups"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users", request.Path);
        Assert.Equal("agent", QueryValue(request, "role"));
        Assert.Equal("50", QueryValue(request, "page%5Bsize%5D"));
        Assert.Equal("cursor-1", QueryValue(request, "page%5Bafter%5D"));
        Assert.Equal("groups", QueryValue(request, "include"));
        // The lean envelope hoists the cursor continuation to the top level and summarizes the rows.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cursor-2", result.GetProperty("after_cursor").GetString());
        var user = result.GetProperty("items")[0];
        Assert.Equal(9, user.GetProperty("id").GetInt64());
        Assert.Equal("Neo", user.GetProperty("name").GetString());
        Assert.False(user.TryGetProperty("created_at", out _));
        // Sideloaded arrays survive under their native names, summary-projected.
        Assert.Equal(31, result.GetProperty("groups")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size_And_Omits_Unset_Filters()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"users":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users", request.Path);
        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("25", QueryValue(request, "page%5Bsize%5D"));
        Assert.Null(QueryValue(request, "role"));
        Assert.Null(QueryValue(request, "page%5Bafter%5D"));
        Assert.Null(QueryValue(request, "include"));
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"users":[{"id":9,"name":"Neo","url":"https://unit-test.zendesk.com/api/v2/users/9.json",
             "user_fields":{"tier":"gold"},"organization_id":null}],"meta":{"has_more":false}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var user = result.GetProperty("items")[0];
        Assert.True(user.TryGetProperty("user_fields", out _)); // the complete record...
        Assert.False(user.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(user.TryGetProperty("organization_id", out _)); // ...and null-valued fields
    }

    [Fact]
    public async Task List_Rejects_An_Invalid_Detail_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.List(detail: "everything", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'summary'", exception.Message);
        Assert.Contains("'full'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Count_Delegates_With_Role()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"count":{"value":123,"refreshed_at":"2024-04-04T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.Count("end-user", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/count", request.Path);
        Assert.Equal("end-user", QueryValue(request, "role"));
        // The whole count object is server-assigned (spec read-only) — it must survive the raw passthrough.
        Assert.Equal(123, result.GetProperty("count").GetProperty("value").GetInt64());
        Assert.Equal("2024-04-04T00:00:00Z", result.GetProperty("count").GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task Autocomplete_Delegates()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"users":[{"id":1},{"id":2}],"count":2}""");
        var tools = CreateTools(harness);

        var result = await tools.Autocomplete("ja", 25, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/autocomplete", request.Path);
        Assert.Equal("ja", QueryValue(request, "name"));
        Assert.Equal("25", QueryValue(request, "per_page"));
        // No offset paging: the endpoint models no 'page' parameter — per_page only caps the suggestion count.
        Assert.Null(QueryValue(request, "page"));
        var items = result.GetProperty("items");
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(2, items[1].GetProperty("id").GetInt64());
        Assert.Equal(2, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Autocomplete_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"users":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Autocomplete("ja", cancellationToken: TestContext.Current.CancellationToken);

        // Type-ahead suggestion lists are small by design — the default 10 is explicit on the wire.
        Assert.Equal("10", QueryValue(harness.Request, "per_page"));
    }

    [Fact]
    public async Task Autocomplete_Rejects_Blank_Name_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Autocomplete(" ", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Related_Delegates()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user_related":{"assigned_tickets":2,"requested_tickets":5}}""");
        var tools = CreateTools(harness);

        var result = await tools.Related(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/related", request.Path);
        // The whole user_related object is server-computed — it must survive the raw passthrough.
        Assert.Equal(2, result.GetProperty("user_related").GetProperty("assigned_tickets").GetInt64());
        Assert.Equal(5, result.GetProperty("user_related").GetProperty("requested_tickets").GetInt64());
    }

    [Fact]
    public async Task Identities_Delegates_With_Cursor_And_Returns_Identity_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"identities":[{"id":301,"user_id":42,"type":"email","value":"jane@example.com","verified":true,
             "primary":true,"created_at":"2024-01-01T00:00:00Z",
             "url":"https://unit-test.zendesk.com/api/v2/users/42/identities/301.json"}],
             "meta":{"has_more":true,"after_cursor":"cursor-3"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Identities(42, 10, "cursor-2", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/identities", request.Path);
        Assert.Equal("10", QueryValue(request, "page%5Bsize%5D"));
        Assert.Equal("cursor-2", QueryValue(request, "page%5Bafter%5D"));
        // Identity rows use the identity summary shape: contact point + primary/verified flags, nothing else.
        var identity = result.GetProperty("items")[0];
        Assert.Equal(301, identity.GetProperty("id").GetInt64());
        Assert.Equal(42, identity.GetProperty("user_id").GetInt64());
        Assert.Equal("email", identity.GetProperty("type").GetString());
        Assert.Equal("jane@example.com", identity.GetProperty("value").GetString());
        Assert.True(identity.GetProperty("verified").GetBoolean());
        Assert.True(identity.GetProperty("primary").GetBoolean());
        Assert.False(identity.TryGetProperty("created_at", out _));
        Assert.False(identity.TryGetProperty("url", out _));
        // The cursor continuation is hoisted to the top of the lean envelope.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cursor-3", result.GetProperty("after_cursor").GetString());
    }

    [Fact]
    public async Task Identities_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"identities":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.Identities(42, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/users/42/identities", harness.Request.Path);
        Assert.Equal("25", QueryValue(harness.Request, "page%5Bsize%5D"));
    }

    [Fact]
    public async Task Groups_Delegates_And_Returns_Group_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"groups":[{"id":201,"name":"Level 2","url":"https://unit-test.zendesk.com/api/v2/groups/201.json",
             "created_at":"2024-01-01T00:00:00Z"}],"count":2}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Groups(42, 1, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/groups", request.Path);
        Assert.Equal("1", QueryValue(request, "page"));
        Assert.Equal("100", QueryValue(request, "per_page"));
        // Group rows use the group summary shape — self-links and timestamps do not appear.
        var group = result.GetProperty("items")[0];
        Assert.Equal(201, group.GetProperty("id").GetInt64());
        Assert.Equal("Level 2", group.GetProperty("name").GetString());
        Assert.False(group.TryGetProperty("url", out _));
        Assert.False(group.TryGetProperty("created_at", out _));
        Assert.Equal(2, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Groups_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"groups":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Groups(42, cancellationToken: TestContext.Current.CancellationToken);

        // The default page size dropped from Zendesk's 100 to the lean 25 — explicit on the wire.
        Assert.Equal("25", QueryValue(harness.Request, "per_page"));
    }

    [Fact]
    public async Task Organizations_Delegates_And_Returns_Organization_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"organizations":[{"id":501,"name":"Acme","created_at":"2024-01-01T00:00:00Z",
             "url":"https://unit-test.zendesk.com/api/v2/organizations/501.json"}],"count":1}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Organizations(42, null, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/organizations", request.Path);
        Assert.Null(QueryValue(request, "page"));
        Assert.Equal("100", QueryValue(request, "per_page"));
        // Organization rows use the organization summary shape (dates stay; self-links do not).
        var organization = result.GetProperty("items")[0];
        Assert.Equal(501, organization.GetProperty("id").GetInt64());
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        Assert.Equal("2024-01-01T00:00:00Z", organization.GetProperty("created_at").GetString());
        Assert.False(organization.TryGetProperty("url", out _));
        Assert.Equal(1, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Organizations_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"organizations":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Organizations(42, cancellationToken: TestContext.Current.CancellationToken);

        // The default page size dropped from Zendesk's 100 to the lean 25 — explicit on the wire.
        Assert.Equal("25", QueryValue(harness.Request, "per_page"));
    }

    [Fact]
    public async Task AssignedTickets_Delegates()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":77}],"count":5}""");
        var tools = CreateTools(harness);

        var result = await tools.AssignedTickets(42, null, 25, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/tickets/assigned", request.Path);
        Assert.Equal("25", QueryValue(request, "per_page"));
        Assert.Null(QueryValue(request, "page"));
        Assert.Null(QueryValue(request, "include"));
        Assert.Equal(77, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(5, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task CcdTickets_Delegates()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":88}],"count":6}""");
        var tools = CreateTools(harness);

        var result = await tools.CcdTickets(42, 2, 50, ["users"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/tickets/ccd", request.Path);
        Assert.Equal("2", QueryValue(request, "page"));
        Assert.Equal("50", QueryValue(request, "per_page"));
        Assert.Equal("users", QueryValue(request, "include"));
        Assert.Equal(88, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(6, result.GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task Tags_Delegates()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["vip"]}""");
        var tools = CreateTools(harness);

        var result = await tools.Tags(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/users/42/tags", request.Path);
        Assert.Equal("vip", result.GetProperty("tags")[0].GetString());
    }
}