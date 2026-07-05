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

public class ZendeskUserWriteToolsTests
{
    private static IMcpExecutionModeAccessor CreateExecutionMode(McpExecutionMode mode = McpExecutionMode.Default)
    {
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return executionMode.Object;
    }

    private static ZendeskUserWriteTools CreateTools(ZendeskToolHarness harness,
        McpExecutionMode mode = McpExecutionMode.Default) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(), CreateExecutionMode(mode));

    [Fact]
    public async Task Create_Posts_User_Envelope_And_Returns_A_Lean_Confirmation()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"user":{"id":7,"name":"Jane","email":"jane@example.com","role":"end-user",
                             "url":"https://unit-test.zendesk.com/api/v2/users/7.json","active":true,
                             "created_at":"2024-01-01T00:00:00Z","tags":["vip"],"user_fields":{"tier":"gold"}}}
                            """);
        var tools = CreateTools(harness);
        var write = new ZendeskUserWrite
        {
            Name = "Jane",
            Email = "jane@example.com",
            Tags = ["vip"],
            UserFields = new Dictionary<string, object?> { ["tier"] = "gold" }
        };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/users", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var user = body.RootElement.GetProperty("user");
        Assert.Equal("Jane", user.GetProperty("name").GetString());
        Assert.Equal("jane@example.com", user.GetProperty("email").GetString());
        Assert.Equal("vip", user.GetProperty("tags")[0].GetString());
        Assert.Equal("gold", user.GetProperty("user_fields").GetProperty("tier").GetString());
        Assert.False(user.TryGetProperty("id", out _)); // unset fields are omitted on the wire
        var json = Assert.IsType<JsonElement>(result);
        // The lean create confirmation: {id, name, email, role, created_at} — nothing else rides along.
        Assert.Equal(7, json.GetProperty("id").GetInt64());
        Assert.Equal("Jane", json.GetProperty("name").GetString());
        Assert.Equal("jane@example.com", json.GetProperty("email").GetString());
        Assert.Equal("end-user", json.GetProperty("role").GetString());
        Assert.Equal("2024-01-01T00:00:00Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("user", out _)); // no envelope wrapper
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("active", out _));
        Assert.False(json.TryGetProperty("tags", out _));
    }

    [Fact]
    public async Task CreateOrUpdate_Posts_To_Create_Or_Update()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"user":{"id":8,"name":"Jane","email":"jane@example.com","external_id":"crm-8",
                             "updated_at":"2024-05-05T00:00:00Z"}}
                            """);
        var tools = CreateTools(harness);
        var write = new ZendeskUserWrite { Email = "jane@example.com", ExternalId = "crm-8" };

        var result = await tools.CreateOrUpdate(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/users/create_or_update", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var user = body.RootElement.GetProperty("user");
        Assert.Equal("jane@example.com", user.GetProperty("email").GetString());
        Assert.Equal("crm-8", user.GetProperty("external_id").GetString());
        var json = Assert.IsType<JsonElement>(result);
        // 200 = an existing user was updated: created:false plus {id, updated_at} and the echo-of-change.
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Equal(8, json.GetProperty("id").GetInt64());
        Assert.Equal("2024-05-05T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.Equal("jane@example.com", json.GetProperty("email").GetString());
        Assert.Equal("crm-8", json.GetProperty("external_id").GetString());
        Assert.False(json.TryGetProperty("name", out _)); // not in the request — not echoed
    }

    [Fact]
    public async Task CreateOrUpdate_Reports_Created_On_201()
    {
        // The upsert answers 200 for an updated user and 201 for a created one — the status carries the
        // created:true|false signal, so the tool must read it instead of discarding it.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"user":{"id":9,"name":"New User","email":"new@example.com","role":"end-user",
                             "created_at":"2024-02-02T00:00:00Z"}}
                            """, HttpStatusCode.Created);
        var tools = CreateTools(harness);

        var result = await tools.CreateOrUpdate(new ZendeskUserWrite { Email = "new@example.com" },
            TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(9, json.GetProperty("id").GetInt64());
        Assert.Equal("new@example.com", json.GetProperty("email").GetString());
        Assert.Equal("2024-02-02T00:00:00Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("updated_at", out _)); // the create shape, not the update shape
    }

    [Fact]
    public async Task CreateMany_Posts_Users_Envelope_As_Job()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"job_status":{"id":"job-1","status":"queued",
                             "url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-1.json"}}
                            """);
        var tools = CreateTools(harness);
        var writes = new[] { new ZendeskUserWrite { Name = "A" }, new ZendeskUserWrite { Name = "B" } };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/users/create_many", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var users = body.RootElement.GetProperty("users");
        Assert.Equal(2, users.GetArrayLength());
        Assert.Equal("A", users[0].GetProperty("name").GetString());
        Assert.Equal("B", users[1].GetProperty("name").GetString());
        var json = Assert.IsType<JsonElement>(result);
        // Bulk jobs collapse to {id, status} — the id is all an agent needs to poll job_statuses_get.
        Assert.Equal("job-1", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task CreateMany_Rejects_More_Than_100_Users()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var writes = Enumerable.Range(0, 101).Select(i => new ZendeskUserWrite { Name = $"U{i}" }).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.CreateMany(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept between 1 and 100 items.", exception.Message);
        Assert.Equal("users", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CreateMany_Throws_When_The_Response_Carries_No_Job_Status()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.CreateMany([new ZendeskUserWrite { Name = "A" }], TestContext.Current.CancellationToken));

        Assert.Contains("job_status", exception.Message);
    }

    [Fact]
    public async Task CreateMany_DryRun_Returns_The_Bulk_Digest()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);
        var writes = new[]
        {
            new ZendeskUserWrite { Name = "A", Email = "a@example.com" },
            new ZendeskUserWrite { Name = "B" }
        };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        // Bulk dry runs digest instead of echoing every write model verbatim: per item, which record is
        // addressed and which fields would change.
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create 2 users", dryRun.Description);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("create", digest["action"]!.GetValue<string>());
        Assert.Equal("users", digest["target"]!.GetValue<string>());
        Assert.Equal(2, digest["count"]!.GetValue<int>());
        var items = digest["items"]!.AsArray();
        Assert.Equal(0, items[0]!["index"]!.GetValue<int>());
        var fields = items[0]!["fields"]!.AsArray().Select(field => field!.GetValue<string>()).ToArray();
        Assert.Contains("name", fields);
        Assert.Contains("email", fields);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CreateOrUpdateMany_Posts_To_Create_Or_Update_Many()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-2","status":"queued"}}""");
        var tools = CreateTools(harness);
        var writes = new[] { new ZendeskUserWrite { Email = "a@example.com" } };

        var result = await tools.CreateOrUpdateMany(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/users/create_or_update_many", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("a@example.com",
            body.RootElement.GetProperty("users")[0].GetProperty("email").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-2", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Update_Puts_User_Envelope()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":42,"name":"Jane","notes":"VIP","updated_at":"2024-06-06T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.Update(42, new ZendeskUserWrite { Notes = "VIP" },
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/42", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var user = body.RootElement.GetProperty("user");
        Assert.Equal("VIP", user.GetProperty("notes").GetString());
        Assert.False(user.TryGetProperty("id", out _)); // unset fields are omitted on the wire
        var json = Assert.IsType<JsonElement>(result);
        // The lean update confirmation: {id, updated_at} plus the server state of the fields sent.
        Assert.Equal(42, json.GetProperty("id").GetInt64());
        Assert.Equal("2024-06-06T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.Equal("VIP", json.GetProperty("notes").GetString());
        Assert.False(json.TryGetProperty("name", out _)); // not in the request — not echoed
        Assert.False(json.TryGetProperty("user", out _)); // no envelope wrapper
    }

    [Fact]
    public async Task Update_Echoes_The_Server_State_Of_The_Requested_Fields()
    {
        // The echo-of-change reads the values back from the SERVER response, so a trigger/business-rule
        // override (here: suspended flipped back, a tag added, a custom field rewritten) is visible without a
        // follow-up users_get. user_fields are post-filtered to the requested keys.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"user":{"id":42,"name":"Jane","suspended":false,"tags":["vip","auto"],
                             "user_fields":{"tier":"silver","other":"x"},"updated_at":"2024-06-07T00:00:00Z"}}
                            """);
        var tools = CreateTools(harness);
        var write = new ZendeskUserWrite
        {
            Suspended = true,
            Tags = ["vip"],
            UserFields = new Dictionary<string, object?> { ["tier"] = "gold" }
        };

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(42, json.GetProperty("id").GetInt64());
        Assert.False(json.GetProperty("suspended").GetBoolean()); // server overrode the requested true
        Assert.Equal(2, json.GetProperty("tags").GetArrayLength());
        Assert.Equal("auto", json.GetProperty("tags")[1].GetString());
        Assert.Equal("silver", json.GetProperty("user_fields").GetProperty("tier").GetString());
        Assert.False(json.GetProperty("user_fields").TryGetProperty("other", out _)); // not requested
        Assert.False(json.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Update_Throws_When_The_Response_Carries_No_User()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Update(42, new ZendeskUserWrite { Notes = "VIP" }, TestContext.Current.CancellationToken));

        Assert.Contains("'user'", exception.Message);
    }

    [Fact]
    public async Task Update_Includes_Id_When_Set()
    {
        // The generated update input has no id field — the tool carries a set Id via AdditionalData so the
        // wire shape matches the old omit-null serializer.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":42}}""");
        var tools = CreateTools(harness);

        await tools.Update(42, new ZendeskUserWrite { Id = 42, Notes = "VIP" },
            TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        Assert.Equal(42, body.RootElement.GetProperty("user").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task UpdateMany_Puts_Shared_Change_With_Ids_Query()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-3","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.UpdateMany([1, 2, 3], new ZendeskUserWrite { Suspended = true },
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/update_many", request.Path);
        Assert.Contains("ids=1%2C2%2C3", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.True(body.RootElement.GetProperty("user").GetProperty("suspended").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("users", out _));
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-3", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateMany_Rejects_More_Than_100_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.UpdateMany(ids, new ZendeskUserWrite { Suspended = true },
                TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept between 1 and 100 items.", exception.Message);
        Assert.Equal("ids", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task UpdateMany_DryRun_Digests_Each_Target_With_The_Changed_Fields()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);

        var result = await tools.UpdateMany([1, 2], new ZendeskUserWrite { Suspended = true },
            TestContext.Current.CancellationToken);

        // The same-change digest pairs every target id with the shared change's field names.
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("update", digest["action"]!.GetValue<string>());
        Assert.Equal(2, digest["count"]!.GetValue<int>());
        var items = digest["items"]!.AsArray();
        Assert.Equal(1, items[0]!["id"]!.GetValue<long>());
        Assert.Equal(2, items[1]!["id"]!.GetValue<long>());
        Assert.Equal("suspended", Assert.Single(items[0]!["fields"]!.AsArray())!.GetValue<string>());
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task UpdateManyBatch_Puts_Users_With_Ids()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-4","status":"queued"}}""");
        var tools = CreateTools(harness);
        var writes = new[]
        {
            new ZendeskUserWrite { Id = 1, Notes = "first" },
            new ZendeskUserWrite { Id = 2, Notes = "second" }
        };

        var result = await tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/update_many", request.Path);
        Assert.DoesNotContain("ids=", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        var users = body.RootElement.GetProperty("users");
        Assert.Equal(1, users[0].GetProperty("id").GetInt64());
        Assert.Equal("first", users[0].GetProperty("notes").GetString());
        Assert.Equal(2, users[1].GetProperty("id").GetInt64());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-4", json.GetProperty("id").GetString());
    }

    [Fact]
    public async Task UpdateManyBatch_Rejects_Item_Missing_Id()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var writes = new[] { new ZendeskUserWrite { Id = 1 }, new ZendeskUserWrite { Notes = "no id" } };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Every batch update item must carry Id.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Merge_Puts_Winner_In_Body_At_Loser_Path()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":9,"name":"Winner","updated_at":"2024-07-07T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.Merge(5, 9, TestContext.Current.CancellationToken);

        // DIRECTION: the path carries the LOSER (absorbed) user; the body carries the WINNER.
        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/5/merge", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(9, body.RootElement.GetProperty("user").GetProperty("id").GetInt64());
        var json = Assert.IsType<JsonElement>(result);
        // The lean merge confirmation: {id, updated_at} of the surviving (winner) user, nothing more.
        Assert.Equal(9, json.GetProperty("id").GetInt64());
        Assert.Equal("2024-07-07T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.False(json.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Delete_Deletes_User_And_Acknowledges_Without_Echoing_The_Record()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"user":{"id":12,"active":false,"name":"Jane","email":"jane@example.com"}}""");
        var tools = CreateTools(harness);

        var result = await tools.Delete(12, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/users/12", request.Path);
        // The soft-deleted record (personal data) is NOT echoed back — the ack carries the structured id.
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Equal(12, acknowledgement.Id);
        Assert.Contains("delete user 12", acknowledgement.Description);
    }

    [Fact]
    public async Task DeleteMany_Deletes_Via_Destroy_Many()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-5","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.DeleteMany([4, 5], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/users/destroy_many", request.Path);
        Assert.Contains("ids=4%2C5", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-5", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteMany_Rejects_Empty_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.DeleteMany([], TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept between 1 and 100 items.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DeleteMany_DryRun_Digests_The_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);

        var result = await tools.DeleteMany([4, 5], TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("delete", digest["action"]!.GetValue<string>());
        Assert.Equal("users", digest["target"]!.GetValue<string>());
        var items = digest["items"]!.AsArray();
        Assert.Equal(4, items[0]!["id"]!.GetValue<long>());
        Assert.Equal(5, items[1]!["id"]!.GetValue<long>());
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DeletePermanently_Deletes_And_Acknowledges_Without_The_Purged_Record()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"deleted_user":{"id":33,"name":"Purged","email":"purged@example.com"}}""");
        var tools = CreateTools(harness);

        var result = await tools.DeletePermanently(33, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/deleted_users/33", request.Path);
        // A GDPR purge must not re-surface the purged user's personal data in its own confirmation.
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Equal(33, acknowledgement.Id);
        Assert.Contains("33", acknowledgement.Description);
    }

    [Fact]
    public async Task IdentitiesCreate_Posts_Identity_Envelope()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"identity":{"id":100,"user_id":42,"type":"email","value":"new@example.com",
                             "verified":false,"primary":false,
                             "url":"https://unit-test.zendesk.com/api/v2/users/42/identities/100.json",
                             "created_at":"2024-01-01T00:00:00Z"}}
                            """);
        var tools = CreateTools(harness);
        var write = new ZendeskUserIdentityWrite { Type = "email", Value = "new@example.com" };

        var result = await tools.IdentitiesCreate(42, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/users/42/identities", request.Path);
        Assert.Contains("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        var identity = body.RootElement.GetProperty("identity");
        Assert.Equal("email", identity.GetProperty("type").GetString());
        Assert.Equal("new@example.com", identity.GetProperty("value").GetString());
        Assert.False(identity.TryGetProperty("primary", out _)); // unset fields are omitted on the wire
        var json = Assert.IsType<JsonElement>(result);
        // The lean create confirmation: {id, user_id, type, value, created_at}.
        Assert.Equal(100, json.GetProperty("id").GetInt64());
        Assert.Equal(42, json.GetProperty("user_id").GetInt64());
        Assert.Equal("email", json.GetProperty("type").GetString());
        Assert.Equal("new@example.com", json.GetProperty("value").GetString());
        Assert.Equal("2024-01-01T00:00:00Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("identity", out _)); // no envelope wrapper
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task IdentitiesUpdate_Puts_Identity_Envelope()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"identity":{"id":100,"user_id":42,"verified":true,"updated_at":"2024-03-03T00:00:00Z"}}""");
        var tools = CreateTools(harness);
        var write = new ZendeskUserIdentityWrite { Verified = true };

        var result = await tools.IdentitiesUpdate(42, 100, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/42/identities/100", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var identity = body.RootElement.GetProperty("identity");
        Assert.True(identity.GetProperty("verified").GetBoolean());
        Assert.False(identity.TryGetProperty("value", out _));
        var json = Assert.IsType<JsonElement>(result);
        // The lean update confirmation: {id, updated_at} plus the server state of the fields sent.
        Assert.Equal(100, json.GetProperty("id").GetInt64());
        Assert.Equal("2024-03-03T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.True(json.GetProperty("verified").GetBoolean());
        Assert.False(json.TryGetProperty("user_id", out _)); // not in the request — not echoed
    }

    [Fact]
    public async Task IdentitiesMakePrimary_Returns_Only_The_Affected_Identity_Row()
    {
        // Collection-level operation: Zendesk answers with the PLURAL identities envelope — the tool
        // post-filters it down to the one row the agent asked about.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""
                            {"identities":[{"id":100,"user_id":42,"type":"email","value":"new@example.com",
                             "primary":true,"verified":true,
                             "url":"https://unit-test.zendesk.com/api/v2/users/42/identities/100.json"},
                             {"id":101,"primary":false}],"count":2}
                            """);
        var tools = CreateTools(harness);

        var result = await tools.IdentitiesMakePrimary(42, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/42/identities/100/make_primary", request.Path);
        Assert.Null(request.Body);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(100, json.GetProperty("id").GetInt64());
        Assert.Equal(42, json.GetProperty("user_id").GetInt64());
        Assert.True(json.GetProperty("primary").GetBoolean());
        Assert.Equal("email", json.GetProperty("type").GetString());
        Assert.False(json.TryGetProperty("identities", out _)); // the full list is not echoed back
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task IdentitiesMakePrimary_Synthesizes_The_Confirmation_When_The_Row_Is_Off_Page()
    {
        // The endpoint returns a single offset page — the promoted identity can fall off it. The confirmation
        // is then synthesized from request facts: a successful call means the identity IS primary now.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"identities":[{"id":101,"user_id":42,"primary":false}],"count":150}""");
        var tools = CreateTools(harness);

        var result = await tools.IdentitiesMakePrimary(42, 100, TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(100, json.GetProperty("id").GetInt64());
        Assert.Equal(42, json.GetProperty("user_id").GetInt64());
        Assert.True(json.GetProperty("primary").GetBoolean());
        Assert.False(json.TryGetProperty("type", out _)); // synthesized — only the request facts are known
    }

    [Fact]
    public async Task IdentitiesVerify_Puts_Verify()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"identity":{"id":100,"user_id":42,"verified":true,"updated_at":"2024-04-04T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.IdentitiesVerify(42, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/42/identities/100/verify", request.Path);
        var json = Assert.IsType<JsonElement>(result);
        // The lean confirmation: {id, updated_at, verified}.
        Assert.Equal(100, json.GetProperty("id").GetInt64());
        Assert.Equal("2024-04-04T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.True(json.GetProperty("verified").GetBoolean());
        Assert.False(json.TryGetProperty("user_id", out _));
    }

    [Fact]
    public async Task IdentitiesRequestVerification_Puts_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.IdentitiesRequestVerification(42, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/42/identities/100/request_verification", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("identity 100", acknowledgement.Description);
        Assert.Equal(100, acknowledgement.Id);
    }

    [Fact]
    public async Task IdentitiesDelete_Deletes_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.NoContent);
        var tools = CreateTools(harness);

        var result = await tools.IdentitiesDelete(42, 100, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/users/42/identities/100", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("identity 100", acknowledgement.Description);
        Assert.Equal(100, acknowledgement.Id);
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_And_Sends_Nothing()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);
        var write = new ZendeskUserWrite { Name = "Jane", Email = "jane@example.com" };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        // Single-entity writes keep the verbatim request echo — small, and the echo IS the verification value.
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create user 'Jane'", dryRun.Description);
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Throws_And_Sends_Nothing()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(12, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}