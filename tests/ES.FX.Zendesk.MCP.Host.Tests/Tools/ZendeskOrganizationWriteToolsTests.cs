using System.Net;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ES.FX.Zendesk.MCP.Host.Tools.Models;
using ModelContextProtocol;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskOrganizationWriteToolsTests
{
    private static (ZendeskOrganizationWriteTools Tools, ZendeskToolHarness Harness) Create(
        McpExecutionMode mode = McpExecutionMode.Default)
    {
        var harness = new ZendeskToolHarness();
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(accessor => accessor.EffectiveMode).Returns(mode);
        return (new ZendeskOrganizationWriteTools(harness.CreateSupportClient(), harness.CreateAdapter(),
            executionMode.Object), harness);
    }

    private static JsonElement ParseBody(ZendeskToolHarness.CapturedRequest request)
    {
        Assert.NotNull(request.Body);
        using var document = JsonDocument.Parse(request.Body);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Create_Posts_Organization_Envelope()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization":{"id":7,"name":"Acme","created_at":"2026-01-01T00:00:00Z","details":"server detail",
             "url":"https://unit-test.zendesk.com/api/v2/organizations/7.json"}}
            """);
        var write = new ZendeskOrganizationWrite
        {
            Name = "Acme",
            Tags = ["vip"],
            OrganizationFields = new Dictionary<string, object?> { ["plan"] = "gold" }
        };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organizations", request.Path);
        var organization = ParseBody(request).GetProperty("organization");
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        Assert.Equal("vip", organization.GetProperty("tags")[0].GetString());
        Assert.Equal("gold", organization.GetProperty("organization_fields").GetProperty("plan").GetString());
        Assert.False(organization.TryGetProperty("id", out _)); // unset fields are omitted
        // The lean create confirmation: {id, name, created_at} — nothing else rides along.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(7, json.GetProperty("id").GetInt64());
        Assert.Equal("Acme", json.GetProperty("name").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("organization", out _));
        Assert.False(json.TryGetProperty("details", out _));
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Create_Throws_When_Zendesk_Returns_No_Organization()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Create(new ZendeskOrganizationWrite { Name = "Acme" }, TestContext.Current.CancellationToken));

        Assert.Contains("no organization", exception.Message);
    }

    [Fact]
    public async Task CreateMany_Posts_Organizations_Envelope()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-1","status":"queued",
             "url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-1.json"}}
            """);
        var writes = new[]
        {
            new ZendeskOrganizationWrite { Name = "A" }, new ZendeskOrganizationWrite { Name = "B" }
        };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organizations/create_many", request.Path);
        var organizations = ParseBody(request).GetProperty("organizations");
        Assert.Equal(2, organizations.GetArrayLength());
        Assert.Equal("A", organizations[0].GetProperty("name").GetString());
        Assert.Equal("B", organizations[1].GetProperty("name").GetString());
        // The lean bulk confirmation: {id, status} — the job's URL and envelope are gone.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-1", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task CreateMany_Rejects_Invalid_Bulk_Count()
    {
        var (tools, harness) = Create();
        var writes = Enumerable.Range(0, 101).Select(i => new ZendeskOrganizationWrite { Name = $"O{i}" })
            .ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.CreateMany(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept between 1 and 100 items.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CreateMany_Throws_When_Zendesk_Returns_No_Job_Status()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("{}");

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.CreateMany([new ZendeskOrganizationWrite { Name = "A" }],
                TestContext.Current.CancellationToken));

        Assert.Contains("no job status", exception.Message);
    }

    [Fact]
    public async Task CreateOrUpdate_Posts_To_Create_Or_Update()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """{"organization":{"id":9,"name":"Acme","external_id":"ext-9","created_at":"2026-01-01T00:00:00Z"}}""",
            HttpStatusCode.Created);
        var write = new ZendeskOrganizationWrite { ExternalId = "ext-9", Name = "Acme" };

        var result = await tools.CreateOrUpdate(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organizations/create_or_update", request.Path);
        var organization = ParseBody(request).GetProperty("organization");
        Assert.Equal("Acme", organization.GetProperty("name").GetString());
        Assert.Equal("ext-9", organization.GetProperty("external_id").GetString());
        // 201 Created → created:true, plus the identity confirmation.
        var json = Assert.IsType<JsonElement>(result);
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(9, json.GetProperty("id").GetInt64());
        Assert.Equal("Acme", json.GetProperty("name").GetString());
        Assert.Equal("ext-9", json.GetProperty("external_id").GetString());
        Assert.Equal("2026-01-01T00:00:00Z", json.GetProperty("created_at").GetString());
    }

    [Fact]
    public async Task CreateOrUpdate_Reports_An_Update_On_200()
    {
        var (tools, harness) = Create();
        // 200 OK (not 201) is Zendesk's "matched and updated an existing organization" signal.
        harness.EnqueueJson(
            """{"organization":{"id":9,"name":"Acme","external_id":"ext-9","updated_at":"2026-02-01T00:00:00Z"}}""");

        var result = await tools.CreateOrUpdate(new ZendeskOrganizationWrite { ExternalId = "ext-9", Name = "Acme" },
            TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        Assert.False(json.GetProperty("created").GetBoolean());
        Assert.Equal(9, json.GetProperty("id").GetInt64());
        Assert.Equal("2026-02-01T00:00:00Z", json.GetProperty("updated_at").GetString());
    }

    [Fact]
    public async Task Update_Puts_Organization_Envelope()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization":{"id":42,"name":"Acme","notes":"vip","domain_names":["acme.com","acme.io"],
             "details":"untouched","updated_at":"2026-03-01T00:00:00Z"}}
            """);
        var write = new ZendeskOrganizationWrite { Notes = "vip", DomainNames = ["acme.com", "acme.io"] };

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/organizations/42", request.Path);
        var organization = ParseBody(request).GetProperty("organization");
        Assert.Equal("vip", organization.GetProperty("notes").GetString());
        Assert.Equal(2, organization.GetProperty("domain_names").GetArrayLength());
        // The update confirmation: {id, updated_at} plus the server-state values of EXACTLY the sent fields...
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(42, json.GetProperty("id").GetInt64());
        Assert.Equal("2026-03-01T00:00:00Z", json.GetProperty("updated_at").GetString());
        Assert.Equal("vip", json.GetProperty("notes").GetString());
        Assert.Equal(2, json.GetProperty("domain_names").GetArrayLength());
        // ...and nothing else — untouched fields don't ride along.
        Assert.False(json.TryGetProperty("name", out _));
        Assert.False(json.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task Update_Echoes_The_Server_State_Of_Requested_Custom_Fields_Only()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization":{"id":42,"updated_at":"2026-03-01T00:00:00Z",
             "organization_fields":{"plan":"silver","region":"eu"}}}
            """);
        var write = new ZendeskOrganizationWrite
        {
            OrganizationFields = new Dictionary<string, object?> { ["plan"] = "gold" }
        };

        var result = await tools.Update(42, write, TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        // The echo carries the SERVER's value of the requested key (a business rule overrode gold → silver)...
        Assert.Equal("silver", json.GetProperty("organization_fields").GetProperty("plan").GetString());
        // ...and only the requested keys — the rest of the custom-field object stays out.
        Assert.False(json.GetProperty("organization_fields").TryGetProperty("region", out _));
    }

    [Fact]
    public async Task UpdateMany_Puts_Same_Change_With_Ids_Query()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-2","status":"queued"}}""");
        var change = new ZendeskOrganizationWrite { Tags = ["vip"] };

        var result = await tools.UpdateMany([1, 2, 3], change, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/organizations/update_many", request.Path);
        Assert.Contains("ids=1%2C2%2C3", request.Query);
        var organization = ParseBody(request).GetProperty("organization");
        Assert.Equal("vip", organization.GetProperty("tags")[0].GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-2", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateManyBatch_Puts_Batch_Envelope()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-3","status":"queued"}}""");
        var writes = new[] { new ZendeskOrganizationWrite { Id = 1, Notes = "a" } };

        var result = await tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/organizations/update_many", request.Path);
        Assert.DoesNotContain("ids=", request.Query);
        var organizations = ParseBody(request).GetProperty("organizations");
        Assert.Equal(1, organizations[0].GetProperty("id").GetInt64());
        Assert.Equal("a", organizations[0].GetProperty("notes").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-3", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateManyBatch_Rejects_Items_Without_Id()
    {
        var (tools, harness) = Create();
        var writes = new[] { new ZendeskOrganizationWrite { Notes = "a" } };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Every batch update item must carry Id.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_Deletes_And_Acknowledges()
    {
        var (tools, harness) = Create();

        var result = await tools.Delete(42, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/organizations/42", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete organization 42", acknowledgement.Description);
        // The affected id is a structured field — the agent never parses it back out of the prose.
        Assert.Equal(42, acknowledgement.Id);
    }

    [Fact]
    public async Task DeleteMany_Deletes_With_Ids_Query()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-4","status":"queued"}}""");

        var result = await tools.DeleteMany([4, 5], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/organizations/destroy_many", request.Path);
        Assert.Contains("ids=4%2C5", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-4", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Merge_Posts_Winner_Id()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization_merge":{"id":"01HPZM6206BF4G63783E5349AD","winner_id":9,"loser_id":5,"status":"new",
             "url":"https://unit-test.zendesk.com/api/v2/organizations/merges/01HPZM6206BF4G63783E5349AD.json"}}
            """);

        var result = await tools.Merge(5, 9, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organizations/5/merge", request.Path);
        var merge = ParseBody(request).GetProperty("organization_merge");
        Assert.Equal(9, merge.GetProperty("winner_id").GetInt64());
        var json = Assert.IsType<JsonElement>(result);
        // The merge id is the merge's own opaque STRING id, not a numeric job_status id.
        Assert.Equal("01HPZM6206BF4G63783E5349AD",
            json.GetProperty("organization_merge").GetProperty("id").GetString());
        Assert.Equal("new", json.GetProperty("organization_merge").GetProperty("status").GetString());
        // The response is already small — the only trim is the API self-link.
        Assert.False(json.GetProperty("organization_merge").TryGetProperty("url", out _));
    }

    [Fact]
    public async Task MembershipsCreate_Posts_Membership_Envelope()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization_membership":{"id":77,"user_id":11,"organization_id":22,"default":true,
             "view_tickets":null,"url":"https://unit-test.zendesk.com/api/v2/organization_memberships/77.json"}}
            """);

        var result = await tools.MembershipsCreate(11, 22, true, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organization_memberships", request.Path);
        var membership = ParseBody(request).GetProperty("organization_membership");
        Assert.Equal(11, membership.GetProperty("user_id").GetInt64());
        Assert.Equal(22, membership.GetProperty("organization_id").GetInt64());
        Assert.True(membership.GetProperty("default").GetBoolean());
        // The confirmation is the unwrapped membership, minus the API self-link and null fields.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(77, json.GetProperty("id").GetInt64());
        Assert.Equal(22, json.GetProperty("organization_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
        Assert.False(json.TryGetProperty("organization_membership", out _));
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("view_tickets", out _));
    }

    [Fact]
    public async Task MembershipsCreate_Omits_Default_When_Unset()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"organization_membership":{"id":78,"user_id":11,"organization_id":22}}""");

        await tools.MembershipsCreate(11, 22, null,
            TestContext.Current.CancellationToken);

        var membership = ParseBody(harness.Request).GetProperty("organization_membership");
        Assert.False(membership.TryGetProperty("default", out _));
    }

    [Fact]
    public async Task MembershipsCreateMany_Posts_Projected_Items()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-5","status":"queued"}}""");
        var memberships = new[]
        {
            new ZendeskOrganizationMembership { Id = 999, UserId = 11, OrganizationId = 22, Default = true }
        };

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/organization_memberships/create_many", request.Path);
        var items = ParseBody(request).GetProperty("organization_memberships");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(11, items[0].GetProperty("user_id").GetInt64());
        Assert.Equal(22, items[0].GetProperty("organization_id").GetInt64());
        Assert.True(items[0].GetProperty("default").GetBoolean());
        // Items are projected — read-model defaults (like Id) never leak into the request.
        Assert.False(items[0].TryGetProperty("id", out _));
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-5", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MembershipsDelete_Deletes_And_Acknowledges()
    {
        var (tools, harness) = Create();

        var result = await tools.MembershipsDelete(77, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/organization_memberships/77", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("delete organization membership 77", acknowledgement.Description);
        Assert.Equal(77, acknowledgement.Id);
    }

    [Fact]
    public async Task MembershipsDeleteMany_Deletes_With_Ids_Query()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson("""{"job_status":{"id":"job-6","status":"queued"}}""");

        var result = await tools.MembershipsDeleteMany([77, 78], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/organization_memberships/destroy_many", request.Path);
        // Zendesk documents a single comma-separated ids value, not repeated ids parameters.
        Assert.Contains("ids=77%2C78", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-6", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task MembershipsMakeDefault_Puts_And_Returns_Only_The_Affected_Membership()
    {
        var (tools, harness) = Create();
        harness.EnqueueJson(
            """
            {"organization_memberships":[
                {"id":77,"user_id":11,"organization_id":22,"default":true,
                 "url":"https://unit-test.zendesk.com/api/v2/organization_memberships/77.json"},
                {"id":78,"user_id":11,"organization_id":23,"default":false}],"count":2}
            """);

        var result = await tools.MembershipsMakeDefault(11, 77, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/users/11/organization_memberships/77/make_default", request.Path);
        // The endpoint returns the user's FULL membership list — the confirmation is post-filtered to only
        // the affected row (minus the API self-link).
        var json = Assert.IsType<JsonElement>(result);
        Assert.False(json.TryGetProperty("organization_memberships", out _));
        Assert.Equal(77, json.GetProperty("id").GetInt64());
        Assert.Equal(11, json.GetProperty("user_id").GetInt64());
        Assert.Equal(22, json.GetProperty("organization_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task MembershipsMakeDefault_Synthesizes_The_Confirmation_When_The_Row_Is_Off_Page()
    {
        var (tools, harness) = Create();
        // A user with many memberships: the affected row is beyond the page Zendesk returned. The write
        // still succeeded, so the confirmation is synthesized from request facts instead of paging.
        harness.EnqueueJson(
            """{"organization_memberships":[{"id":78,"user_id":11,"organization_id":23,"default":false}],"count":1}""");

        var result = await tools.MembershipsMakeDefault(11, 77, TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(77, json.GetProperty("id").GetInt64());
        Assert.Equal(11, json.GetProperty("user_id").GetInt64());
        Assert.True(json.GetProperty("default").GetBoolean());
        // organization_id is a server fact the response did not confirm — it is never guessed.
        Assert.False(json.TryGetProperty("organization_id", out _));
    }

    [Fact]
    public async Task DryRun_Returns_DryRunResult_Without_Calling_Zendesk()
    {
        var write = new ZendeskOrganizationWrite { Name = "Acme" };
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("create organization 'Acme'", dryRun.Description);
        // Single-entity writes keep the verbatim echo — that IS the verification value.
        Assert.Same(write, dryRun.Request);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_CreateMany_Returns_A_Digest_Instead_Of_The_Verbatim_Payload()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var writes = new[]
        {
            new ZendeskOrganizationWrite { Name = "A", ExternalId = "ext-A" },
            new ZendeskOrganizationWrite { Name = "B" }
        };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.Empty(harness.Requests);
        var digest = JsonSerializer.SerializeToElement(dryRun.Request);
        Assert.Equal("create", digest.GetProperty("action").GetString());
        Assert.Equal("organizations", digest.GetProperty("target").GetString());
        Assert.Equal(2, digest.GetProperty("count").GetInt32());
        // Per item: position, recognizable identity, and the names of the fields that would be sent.
        var first = digest.GetProperty("items")[0];
        Assert.Equal(0, first.GetProperty("index").GetInt32());
        Assert.Equal("ext-A", first.GetProperty("external_id").GetString());
        Assert.Contains("name", first.GetProperty("fields").EnumerateArray().Select(field => field.GetString()));
    }

    [Fact]
    public async Task DryRun_CreateMany_Still_Rejects_Invalid_Bulk_Count()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var writes = Enumerable.Range(0, 101).Select(i => new ZendeskOrganizationWrite { Name = $"O{i}" })
            .ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.CreateMany(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept between 1 and 100 items.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_UpdateMany_Digest_Carries_The_Target_Ids_And_Changed_Field_Names()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var change = new ZendeskOrganizationWrite { Tags = ["vip"], Notes = "check" };

        var result = await tools.UpdateMany([1, 2], change, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.Empty(harness.Requests);
        var digest = JsonSerializer.SerializeToElement(dryRun.Request);
        Assert.Equal("update", digest.GetProperty("action").GetString());
        Assert.Equal("organizations", digest.GetProperty("target").GetString());
        Assert.Equal(2, digest.GetProperty("count").GetInt32());
        // The shared change is merged per target: {index, id, fields:[changed field names]}.
        var first = digest.GetProperty("items")[0];
        Assert.Equal(0, first.GetProperty("index").GetInt32());
        Assert.Equal(1, first.GetProperty("id").GetInt64());
        var fields = first.GetProperty("fields").EnumerateArray().Select(field => field.GetString()).ToArray();
        Assert.Contains("tags", fields);
        Assert.Contains("notes", fields);
        Assert.DoesNotContain("name", fields);
        Assert.Equal(2, digest.GetProperty("items")[1].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task DryRun_UpdateManyBatch_Still_Rejects_Items_Without_Id()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var writes = new[] { new ZendeskOrganizationWrite { Notes = "a" } };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.UpdateManyBatch(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Every batch update item must carry Id.", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_DeleteMany_Digest_Lists_The_Ids()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);

        var result = await tools.DeleteMany([4, 5], TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.Empty(harness.Requests);
        var digest = JsonSerializer.SerializeToElement(dryRun.Request);
        Assert.Equal("delete", digest.GetProperty("action").GetString());
        Assert.Equal("organizations", digest.GetProperty("target").GetString());
        Assert.Equal(2, digest.GetProperty("count").GetInt32());
        Assert.Equal(4, digest.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(5, digest.GetProperty("items")[1].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task DryRun_MembershipsCreateMany_Digest_Identifies_Rows_By_User_And_Organization()
    {
        var (tools, harness) = Create(McpExecutionMode.DryRun);
        var memberships = new[]
        {
            new ZendeskOrganizationMembership { UserId = 11, OrganizationId = 22, Default = true }
        };

        var result = await tools.MembershipsCreateMany(memberships, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.Empty(harness.Requests);
        Assert.Contains("create 1 organization memberships", dryRun.Description);
        var digest = JsonSerializer.SerializeToElement(dryRun.Request);
        Assert.Equal("create", digest.GetProperty("action").GetString());
        Assert.Equal("organization_memberships", digest.GetProperty("target").GetString());
        Assert.Equal(1, digest.GetProperty("count").GetInt32());
        // A membership's identity is its user_id/organization_id pair — both ride on the digest row.
        var row = digest.GetProperty("items")[0];
        Assert.Equal(0, row.GetProperty("index").GetInt32());
        Assert.Equal(11, row.GetProperty("user_id").GetInt64());
        Assert.Equal(22, row.GetProperty("organization_id").GetInt64());
        Assert.True(row.GetProperty("default").GetBoolean());
    }

    [Fact]
    public async Task ReadOnly_Rejects_Without_Calling_Zendesk()
    {
        var (tools, harness) = Create(McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(42, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}