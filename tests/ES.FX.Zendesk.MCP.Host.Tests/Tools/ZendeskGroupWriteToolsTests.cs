using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskGroupWriteToolsTests
{
    private static (ZendeskGroupWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(accessor => accessor.EffectiveMode).Returns(mode);
        return (new ZendeskGroupWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    [Fact]
    public async Task Create_Delegates_And_Returns_A_Minimal_Confirmation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group":{"url":"https://unit-test.zendesk.com/api/v2/groups/5.json","id":5,"name":"Tier 3",
            "is_public":false,"created_at":"2026-01-02T03:04:05Z","updated_at":"2026-01-02T03:04:05Z"}}
            """);
        var write = new ZendeskGroupWrite { Name = "Tier 3", IsPublic = false };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/groups", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var group = body.RootElement.GetProperty("group");
        Assert.Equal("Tier 3", group.GetProperty("name").GetString());
        Assert.False(group.GetProperty("is_public").GetBoolean());
        // Unset curated fields must be omitted on the wire (parity with the old omit-null serializer).
        Assert.False(group.TryGetProperty("description", out _));
        // The lean confirmation: id + identity fields + created_at — groups_get is the full sink.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(5, json.GetProperty("id").GetInt64());
        Assert.Equal("Tier 3", json.GetProperty("name").GetString());
        Assert.False(json.GetProperty("is_public").GetBoolean());
        Assert.Equal("2026-01-02T03:04:05Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task Update_Delegates_And_Echoes_The_Server_State_Of_The_Requested_Fields()
    {
        var (tools, harness) = Create();
        // The server-state description deliberately differs from the request — the echo-of-change must
        // report what Zendesk actually stored, not what was sent.
        harness.EnqueueJson(
            """
            {"group":{"id":5,"name":"Tier 3","description":"Escalations (EMEA)","is_public":true,
            "updated_at":"2026-02-03T04:05:06Z"}}
            """);
        var write = new ZendeskGroupWrite { Description = "Escalations" };

        var result = await tools.Update(5, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/groups/5", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var group = body.RootElement.GetProperty("group");
        Assert.Equal("Escalations", group.GetProperty("description").GetString());
        Assert.False(group.TryGetProperty("name", out _));
        Assert.False(group.TryGetProperty("is_public", out _));
        // Echo-of-change: {id, updated_at} plus the server-state values of exactly the requested fields.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(5, json.GetProperty("id").GetInt64());
        Assert.Equal("2026-02-03T04:05:06Z", json.GetProperty("updated_at").GetString());
        Assert.Equal("Escalations (EMEA)", json.GetProperty("description").GetString());
        // Fields the request did not set are not echoed, however the server returned them.
        Assert.False(json.TryGetProperty("name", out _));
        Assert.False(json.TryGetProperty("is_public", out _));
    }

    [Fact]
    public async Task Delete_Delegates_And_Acknowledges_With_The_Affected_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.Delete(5, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/groups/5", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete group 5", acknowledgement.Description);
        // The affected id is structured — the agent must not have to parse it out of the prose.
        Assert.Equal(5, acknowledgement.Id);
    }

    [Fact]
    public async Task MembershipsCreate_Delegates_And_Returns_A_Minimal_Confirmation()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group_membership":{"id":88,"url":"https://unit-test.zendesk.com/api/v2/group_memberships/88.json",
            "user_id":11,"group_id":5,"default":true,"created_at":"2026-01-02T03:04:05Z"}}
            """);

        var result = await tools.MembershipsCreate(11, 5, true, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/group_memberships", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var membership = body.RootElement.GetProperty("group_membership");
        Assert.Equal(11, membership.GetProperty("user_id").GetInt64());
        Assert.Equal(5, membership.GetProperty("group_id").GetInt64());
        Assert.True(membership.GetProperty("default").GetBoolean());
        // The lean confirmation carries the created membership's routing identity only.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(88, json.GetProperty("id").GetInt64());
        Assert.Equal(11, json.GetProperty("user_id").GetInt64());
        Assert.Equal(5, json.GetProperty("group_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
        Assert.Equal("2026-01-02T03:04:05Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task MembershipsCreate_Omits_Unset_Default()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"group_membership":{"id":88,"user_id":11,"group_id":5}}""");

        await tools.MembershipsCreate(11, 5, cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var membership = body.RootElement.GetProperty("group_membership");
        Assert.False(membership.TryGetProperty("default", out _));
    }

    [Fact]
    public async Task MembershipsCreateMany_Delegates_And_Returns_The_Job_Handle()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-1","status":"queued",
            "url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-1.json"}}
            """);
        var memberships = new[]
        {
            new ZendeskGroupMembership
            {
                Id = 999, UserId = 11, GroupId = 5, Default = true, CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/group_memberships/create_many", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var item = body.RootElement.GetProperty("group_memberships")[0];
        Assert.Equal(11, item.GetProperty("user_id").GetInt64());
        Assert.Equal(5, item.GetProperty("group_id").GetInt64());
        Assert.True(item.GetProperty("default").GetBoolean());
        // Read-model defaults (e.g. Id, CreatedAt) must never leak into the request payload.
        Assert.False(item.TryGetProperty("id", out _));
        Assert.False(item.TryGetProperty("created_at", out _));
        // The bulk confirmation is the job handle only — job_statuses_get carries the job's state.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-1", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task MembershipsCreateMany_DryRun_Returns_A_Digest_Not_A_Verbatim_Echo()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var memberships = new[]
        {
            new ZendeskGroupMembership { Id = 999, UserId = 11, GroupId = 5, Default = true },
            new ZendeskGroupMembership { UserId = 12, GroupId = 5 }
        };

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("create", digest["action"]!.GetValue<string>());
        Assert.Equal("group_memberships", digest["target"]!.GetValue<string>());
        Assert.Equal(2, digest["count"]!.GetValue<int>());
        var items = Assert.IsType<JsonArray>(digest["items"]);
        var first = Assert.IsType<JsonObject>(items[0]);
        Assert.Equal(0, first["index"]!.GetValue<int>());
        // The digest reports the PROJECTED wire payload — read-model defaults (Id/CreatedAt) never leak.
        Assert.Null(first["id"]);
        var firstFields = Assert.IsType<JsonArray>(first["fields"])
            .Select(field => field!.GetValue<string>()).ToArray();
        Assert.Contains("user_id", firstFields);
        Assert.Contains("group_id", firstFields);
        Assert.Contains("default", firstFields);
        Assert.DoesNotContain("created_at", firstFields);
        var secondFields = Assert.IsType<JsonArray>(Assert.IsType<JsonObject>(items[1])["fields"])
            .Select(field => field!.GetValue<string>()).ToArray();
        // Unset fields count as absent — they are omitted from the wire format too.
        Assert.DoesNotContain("default", secondFields);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task MembershipsCreateMany_Rejects_Invalid_Bulk_Count(int count)
    {
        var (tools, harness) = Create();
        var memberships = Enumerable.Range(0, count)
            .Select(i => new ZendeskGroupMembership { UserId = i, GroupId = 5 })
            .ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken));

        Assert.Contains("between 1 and 100", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task MembershipsDelete_Delegates_And_Acknowledges_With_The_Affected_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueStatus(HttpStatusCode.NoContent);

        var result = await tools.MembershipsDelete(88, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/group_memberships/88", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete group membership 88", acknowledgement.Description);
        Assert.Equal(88, acknowledgement.Id);
    }

    [Fact]
    public async Task MembershipsDeleteMany_Delegates_And_Returns_The_Job_Handle()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-2","status":"queued","progress":null}}""");

        var result = await tools.MembershipsDeleteMany([88, 89], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/group_memberships/destroy_many", request.Path);
        Assert.Contains("ids=88%2C89", request.Query);
        // The bulk confirmation is the job handle only — job_statuses_get carries the job's state.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-2", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
    }

    [Fact]
    public async Task MembershipsDeleteMany_DryRun_Returns_An_Id_Digest()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.MembershipsDeleteMany([88, 89], TestContext.Current.CancellationToken);

        Assert.Empty(harness.Requests);
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("delete", digest["action"]!.GetValue<string>());
        Assert.Equal("group_memberships", digest["target"]!.GetValue<string>());
        Assert.Equal(2, digest["count"]!.GetValue<int>());
        var items = Assert.IsType<JsonArray>(digest["items"]);
        Assert.Equal(88, items[0]!["id"]!.GetValue<long>());
        Assert.Equal(89, items[1]!["id"]!.GetValue<long>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task MembershipsDeleteMany_Rejects_Invalid_Bulk_Count(int count)
    {
        var (tools, harness) = Create();
        var membershipIds = Enumerable.Range(0, count).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.MembershipsDeleteMany(membershipIds, TestContext.Current.CancellationToken));

        Assert.Contains("between 1 and 100", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task MembershipsMakeDefault_Returns_Only_The_Affected_Row()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"group_memberships":[
            {"id":88,"url":"https://unit-test.zendesk.com/api/v2/group_memberships/88.json","user_id":11,
            "group_id":5,"default":true},
            {"id":89,"user_id":11,"group_id":6,"default":false}],"count":2}
            """);

        var result = await tools.MembershipsMakeDefault(11, 88, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/11/group_memberships/88/make_default", request.Path);
        // The endpoint echoes the user's FULL membership list; only the affected row is returned (full view).
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(88, json.GetProperty("id").GetInt64());
        Assert.Equal(11, json.GetProperty("user_id").GetInt64());
        Assert.Equal(5, json.GetProperty("group_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("group_memberships", out _));
    }

    [Fact]
    public async Task MembershipsMakeDefault_Synthesizes_The_Confirmation_When_The_Row_Is_Off_Page()
    {
        var (tools, harness) = Create();
        // The echoed list is paginated — a busy agent's affected membership may not be on the returned page.
        harness.EnqueueJson(
            """
            {"group_memberships":[{"id":89,"user_id":11,"group_id":6,"default":false}],"count":150,
            "next_page":"https://unit-test.zendesk.com/api/v2/users/11/group_memberships.json?page=2"}
            """);

        var result = await tools.MembershipsMakeDefault(11, 88, TestContext.Current.CancellationToken);

        // The write succeeded (a failure would have thrown), so the confirmation is known from request facts.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(88, json.GetProperty("id").GetInt64());
        Assert.Equal(11, json.GetProperty("user_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskGroupWrite { Name = "Tier 3" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create group 'Tier 3'", dryRun.Description);
        // Single-entity writes keep the verbatim echo — small, and the echo IS the verification value.
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(5, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}