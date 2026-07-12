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

public class ZendeskTicketWriteToolsTests
{
    private static IMcpExecutionModeAccessor CreateExecutionMode(McpExecutionMode mode = McpExecutionMode.Default)
    {
        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(m => m.EffectiveMode).Returns(mode);
        return executionMode.Object;
    }

    private static ZendeskTicketWriteTools CreateTools(ZendeskToolHarness harness,
        McpExecutionMode mode = McpExecutionMode.Default) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(), CreateExecutionMode(mode));

    [Fact]
    public async Task Create_Posts_Ticket_Envelope_And_Returns_The_Lean_Confirmation()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"ticket":{"id":1,"url":"https://unit-test.zendesk.com/api/v2/tickets/1.json","subject":"Broken widget",
             "status":"new","created_at":"2026-07-01T00:00:00Z","description":"It broke.","tags":["vip"]},
             "audit":{"id":9001,"events":[{"id":1,"type":"Create"}]}}
            """);
        var tools = CreateTools(harness);
        var write = new ZendeskTicketWrite
        {
            Subject = "Broken widget",
            Comment = new ZendeskTicketCommentWrite { Body = "It broke.", Public = false, Uploads = ["tok1"] },
            Status = "new",
            Priority = "high",
            Type = "question",
            RequesterId = 55,
            Tags = ["vip"],
            CustomFields =
            [
                new ZendeskCustomFieldWrite { Id = 123, Value = "gold" },
                new ZendeskCustomFieldWrite { Id = 124, Value = JsonSerializer.SerializeToElement(new[] { "a", "b" }) }
            ]
        };

        var result = await tools.Create(write, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/tickets", request.Path);
        Assert.Contains("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        var ticket = body.RootElement.GetProperty("ticket");
        Assert.Equal("Broken widget", ticket.GetProperty("subject").GetString());
        Assert.Equal("It broke.", ticket.GetProperty("comment").GetProperty("body").GetString());
        Assert.False(ticket.GetProperty("comment").GetProperty("public").GetBoolean());
        Assert.Equal("tok1", ticket.GetProperty("comment").GetProperty("uploads")[0].GetString());
        Assert.Equal("new", ticket.GetProperty("status").GetString());
        Assert.Equal("high", ticket.GetProperty("priority").GetString());
        Assert.Equal("question", ticket.GetProperty("type").GetString());
        Assert.Equal(55, ticket.GetProperty("requester_id").GetInt64());
        Assert.Equal("vip", ticket.GetProperty("tags")[0].GetString());
        var customFields = ticket.GetProperty("custom_fields");
        Assert.Equal(123, customFields[0].GetProperty("id").GetInt64());
        Assert.Equal("gold", customFields[0].GetProperty("value").GetString());
        Assert.Equal("b", customFields[1].GetProperty("value")[1].GetString());
        Assert.False(ticket.TryGetProperty("id", out _)); // unset fields are omitted on the wire
        Assert.False(ticket.TryGetProperty("assignee_id", out _));
        // The lean confirmation: {id, subject, status, created_at, audit_id} — the created audit's id is kept,
        // the audit member itself (an echo of the request just sent) is stripped, as is the ticket envelope.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(1, json.GetProperty("id").GetInt64());
        Assert.Equal("Broken widget", json.GetProperty("subject").GetString());
        Assert.Equal("new", json.GetProperty("status").GetString());
        Assert.Equal("2026-07-01T00:00:00Z", json.GetProperty("created_at").GetString());
        Assert.Equal(9001, json.GetProperty("audit_id").GetInt64());
        Assert.False(json.TryGetProperty("ticket", out _));
        Assert.False(json.TryGetProperty("audit", out _));
        Assert.False(json.TryGetProperty("description", out _));
        Assert.False(json.TryGetProperty("tags", out _));
    }

    [Fact]
    public async Task CreateMany_Posts_Tickets_Envelope_As_Job()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-1","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-1.json",
             "status":"queued","total":null,"progress":null}}
            """);
        var tools = CreateTools(harness);
        var writes = new[]
        {
            new ZendeskTicketWrite { Subject = "A" },
            new ZendeskTicketWrite { Subject = "B" }
        };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/tickets/create_many", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var tickets = body.RootElement.GetProperty("tickets");
        Assert.Equal(2, tickets.GetArrayLength());
        Assert.Equal("A", tickets[0].GetProperty("subject").GetString());
        Assert.Equal("B", tickets[1].GetProperty("subject").GetString());
        // The lean job confirmation: {id, status}, unwrapped, with the API self-link and null members gone.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-1", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task CreateMany_Rejects_More_Than_100_Tickets()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var writes = Enumerable.Range(0, 101).Select(i => new ZendeskTicketWrite { Subject = $"T{i}" }).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.CreateMany(writes, TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept 1–100 items per call", exception.Message);
        Assert.Equal("tickets", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Create_Rejects_Tag_Deltas_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Create(new ZendeskTicketWrite { Subject = "A", RemoveTags = ["b"] },
                TestContext.Current.CancellationToken));

        Assert.Contains("additional_tags/remove_tags are not supported", exception.Message);
        // The redirect points at the surviving bulk tag-delta tools.
        Assert.Contains("tickets_tags_add_many", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task CreateMany_Rejects_Tag_Deltas_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var writes = new[]
        {
            new ZendeskTicketWrite { Subject = "A" },
            new ZendeskTicketWrite { Subject = "B", AdditionalTags = ["x"] }
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.CreateMany(writes, TestContext.Current.CancellationToken));

        Assert.Contains("additional_tags/remove_tags are not supported", exception.Message);
        Assert.Empty(harness.Requests);
    }

    // ── Single-action ticket setters (decomposed from the former tickets_update) ──────────────────────────────

    [Fact]
    public async Task StatusSet_Puts_Status_And_Returns_The_Echo_Of_Change()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"ticket":{"id":42,"status":"open","updated_at":"2026-07-01T10:00:05Z"},"audit":{"id":1001}}""");
        var tools = CreateTools(harness);

        var result = await tools.StatusSet(42, "solved", cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/42", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("solved", body.RootElement.GetProperty("ticket").GetProperty("status").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(42, json.GetProperty("id").GetInt64());
        Assert.Equal("2026-07-01T10:00:05Z", json.GetProperty("updated_at").GetString());
        Assert.Equal(1001, json.GetProperty("audit_id").GetInt64());
        // Echo-of-change: the SERVER value of the one field set — a trigger kept it 'open' despite 'solved'.
        Assert.Equal("open", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task StatusSet_Applies_The_Optimistic_Lock_Stamp()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":42,"status":"solved"}}""");
        var tools = CreateTools(harness);
        var stamp = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        await tools.StatusSet(42, "solved", updatedStamp: stamp,
            cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var ticket = body.RootElement.GetProperty("ticket");
        Assert.True(ticket.GetProperty("safe_update").GetBoolean());
        Assert.Equal(stamp, ticket.GetProperty("updated_stamp").GetDateTimeOffset());
    }

    [Fact]
    public async Task StatusSet_Passes_Unknown_Status_Through_Verbatim()
    {
        // Known status/priority/type strings map onto the generated enums; anything else is sent as-is so
        // Zendesk stays the validator (parity with the composite it replaced).
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":42}}""");
        var tools = CreateTools(harness);

        await tools.StatusSet(42, "reopened", cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        Assert.Equal("reopened", body.RootElement.GetProperty("ticket").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReplyPublic_Puts_A_Public_Comment()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":42,"updated_at":"2026-07-01T10:00:05Z"},"audit":{"id":1002}}""");
        var tools = CreateTools(harness);

        var result = await tools.ReplyPublic(42, "Thanks, fixed!", uploads: ["tok1"],
            cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/42", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        var comment = body.RootElement.GetProperty("ticket").GetProperty("comment");
        Assert.Equal("Thanks, fixed!", comment.GetProperty("body").GetString());
        Assert.True(comment.GetProperty("public").GetBoolean());
        Assert.Equal("tok1", comment.GetProperty("uploads")[0].GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(1002, json.GetProperty("audit_id").GetInt64());
    }

    [Fact]
    public async Task NoteAdd_Puts_An_Internal_Comment()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":42}}""");
        var tools = CreateTools(harness);

        await tools.NoteAdd(42, "Internal only.", cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var comment = body.RootElement.GetProperty("ticket").GetProperty("comment");
        Assert.Equal("Internal only.", comment.GetProperty("body").GetString());
        // The whole reason for the split: a note is NEVER public.
        Assert.False(comment.GetProperty("public").GetBoolean());
    }

    [Fact]
    public async Task ReplyPublic_Rejects_Missing_Body_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ReplyPublic(42, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("exactly one of body or htmlBody", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task AssigneeSet_Puts_Assignee_And_Optional_Group()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":42,"assignee_id":7}}""");
        var tools = CreateTools(harness);

        await tools.AssigneeSet(42, 7, 3, cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var ticket = body.RootElement.GetProperty("ticket");
        Assert.Equal(7, ticket.GetProperty("assignee_id").GetInt64());
        Assert.Equal(3, ticket.GetProperty("group_id").GetInt64());
    }

    [Fact]
    public async Task CustomFieldsSet_Echoes_Only_The_Requested_Field()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"ticket":{"id":42,"custom_fields":[{"id":100,"value":"gold"},{"id":200,"value":"secret"}]}}""");
        var tools = CreateTools(harness);

        var result = await tools.CustomFieldsSet(42,
            [new ZendeskCustomFieldWrite { Id = 100, Value = "gold" }],
            cancellationToken: TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonElement>(result);
        var echoed = json.GetProperty("custom_fields");
        // Only the field the request set is echoed — the unrelated custom field (200) is not leaked.
        Assert.Equal(1, echoed.GetArrayLength());
        Assert.Equal(100, echoed[0].GetProperty("id").GetInt64());
    }

    // ── Bulk single-action ticket setters ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusSetMany_Puts_Shared_Status_With_Ids_Query()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-2","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.StatusSetMany([1, 2, 3], "solved", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/update_many", request.Path);
        Assert.Contains("ids=1%2C2%2C3", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("solved", body.RootElement.GetProperty("ticket").GetProperty("status").GetString());
        Assert.False(body.RootElement.TryGetProperty("tickets", out _));
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-2", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TagsAddMany_Puts_Additional_Tags()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-3","status":"queued"}}""");
        var tools = CreateTools(harness);

        await tools.TagsAddMany([1, 2], ["escalated"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal("/api/v2/tickets/update_many", request.Path);
        Assert.Contains("ids=1%2C2", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("escalated",
            body.RootElement.GetProperty("ticket").GetProperty("additional_tags")[0].GetString());
    }

    [Fact]
    public async Task ReplyPublicMany_Puts_A_Public_Comment_To_Many()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-4","status":"queued"}}""");
        var tools = CreateTools(harness);

        await tools.ReplyPublicMany([1, 2], "Bulk reply.",
            cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        var comment = body.RootElement.GetProperty("ticket").GetProperty("comment");
        Assert.Equal("Bulk reply.", comment.GetProperty("body").GetString());
        Assert.True(comment.GetProperty("public").GetBoolean());
    }

    [Fact]
    public async Task NoteAddMany_Rejects_Missing_Body_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.NoteAddMany([1, 2], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("exactly one of body or htmlBody", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task StatusSetMany_Rejects_More_Than_100_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.StatusSetMany(ids, "open", TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept 1–100 items per call", exception.Message);
        Assert.Equal("ids", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Delete_Deletes_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.NoContent);
        var tools = CreateTools(harness);

        var result = await tools.Delete(7, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/tickets/7", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("soft-delete ticket 7", acknowledgement.Description);
        // The affected id rides structured on the acknowledgement, not just in the description prose.
        Assert.Equal(7, acknowledgement.Id);
    }

    [Fact]
    public async Task DeleteMany_Deletes_Via_Destroy_Many()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-4","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.DeleteMany([7, 8], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/tickets/destroy_many", request.Path);
        Assert.Contains("ids=7%2C8", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-4", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteMany_Rejects_Empty_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.DeleteMany([], TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept 1–100 items per call", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Merge_Posts_Bare_Payload_At_Target()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"job_status":{"id":"job-5","url":"https://unit-test.zendesk.com/api/v2/job_statuses/job-5.json",
             "status":"queued"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Merge(9, [5, 6], "target note", "source note", true, false,
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/tickets/9/merge", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        // QUIRK: the merge payload is a BARE object — no "ticket" envelope.
        Assert.False(body.RootElement.TryGetProperty("ticket", out _));
        var ids = body.RootElement.GetProperty("ids");
        Assert.Equal(5, ids[0].GetInt64());
        Assert.Equal(6, ids[1].GetInt64());
        Assert.Equal("target note", body.RootElement.GetProperty("target_comment").GetString());
        Assert.Equal("source note", body.RootElement.GetProperty("source_comment").GetString());
        Assert.True(body.RootElement.GetProperty("target_comment_is_public").GetBoolean());
        Assert.False(body.RootElement.GetProperty("source_comment_is_public").GetBoolean());
        // The merge job's lean confirmation: {id, status}, unwrapped, API self-link gone.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-5", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
        Assert.False(json.TryGetProperty("job_status", out _));
        Assert.False(json.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Merge_Carries_64Bit_Source_Ids()
    {
        // The generated merge input types ids as int — the tool must not truncate 64-bit ticket ids.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-6"}}""");
        var tools = CreateTools(harness);

        await tools.Merge(9, [3_000_000_000], cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        Assert.Equal(3_000_000_000, body.RootElement.GetProperty("ids")[0].GetInt64());
    }

    [Fact]
    public async Task Merge_Rejects_Empty_Sources()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Merge(9, [], cancellationToken: TestContext.Current.CancellationToken));

        Assert.StartsWith("At least one source ticket id is required.", exception.Message);
        Assert.Equal("sourceTicketIds", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task MarkSpam_Puts_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.MarkSpam(11, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/11/mark_as_spam", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("mark ticket 11 as spam", acknowledgement.Description);
        Assert.Equal(11, acknowledgement.Id);
    }

    [Fact]
    public async Task MarkSpamMany_Puts_With_Ids_Query()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-7","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.MarkSpamMany([11, 12], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/mark_many_as_spam", request.Path);
        Assert.Contains("ids=11%2C12", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-7", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Restore_Puts_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.Restore(13, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/deleted_tickets/13/restore", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("restore soft-deleted ticket 13", acknowledgement.Description);
        Assert.Equal(13, acknowledgement.Id);
    }

    [Fact]
    public async Task RestoreMany_Puts_With_Ids_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.RestoreMany([13, 14], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/deleted_tickets/restore_many", request.Path);
        Assert.Contains("ids=13%2C14", request.Query);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        // The restored ids ride structured on the acknowledgement.
        Assert.Equal(new long[] { 13, 14 }, acknowledgement.Ids);
    }

    [Fact]
    public async Task DeletePermanently_Deletes_Deleted_Ticket_And_Returns_Job()
    {
        // QUIRK: the permanent delete is async even for a single ticket — a job comes back.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-8","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.DeletePermanently(15, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/deleted_tickets/15", request.Path);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-8", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeletePermanentlyMany_Deletes_Via_Destroy_Many()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-9","status":"queued"}}""");
        var tools = CreateTools(harness);

        var result = await tools.DeletePermanentlyMany([15, 16], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/deleted_tickets/destroy_many", request.Path);
        Assert.Contains("ids=15%2C16", request.Query);
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-9", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TagsSet_Posts_Tags_Body()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["vip","billing"]}""");
        var tools = CreateTools(harness);

        var result = await tools.TagsSet(21, ["vip", "billing"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/tickets/21/tags", request.Path);
        Assert.Contains("application/json", request.ContentType);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("vip", body.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal("billing", body.RootElement.GetProperty("tags")[1].GetString());
        Assert.False(body.RootElement.TryGetProperty("updated_stamp", out _));
        Assert.False(body.RootElement.TryGetProperty("safe_update", out _));
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("vip", json.GetProperty("tags")[0].GetString());
    }

    [Fact]
    public async Task TagsAdd_Puts_Tags_With_Safe_Update_Stamp()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["vip","urgent"]}""");
        var tools = CreateTools(harness);
        var stamp = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        var result = await tools.TagsAdd(21, ["urgent"], stamp, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/21/tags", request.Path);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("urgent", body.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal(stamp, body.RootElement.GetProperty("updated_stamp").GetDateTimeOffset());
        // The tags docs pass safe_update as the STRING "true", not a boolean.
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("safe_update").ValueKind);
        Assert.Equal("true", body.RootElement.GetProperty("safe_update").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("urgent", json.GetProperty("tags")[1].GetString());
    }

    [Fact]
    public async Task TagsAdd_Omits_Safe_Update_Without_Stamp()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["urgent"]}""");
        var tools = CreateTools(harness);

        await tools.TagsAdd(21, ["urgent"], cancellationToken: TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(harness.Request.Body!);
        Assert.False(body.RootElement.TryGetProperty("updated_stamp", out _));
        Assert.False(body.RootElement.TryGetProperty("safe_update", out _));
    }

    [Fact]
    public async Task TagsRemove_Deletes_With_Body()
    {
        // A DELETE carrying a JSON body — the documented shape of the tag-removal endpoint.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tags":["vip"]}""");
        var tools = CreateTools(harness);
        var stamp = new DateTimeOffset(2026, 7, 2, 11, 0, 0, TimeSpan.Zero);

        var result = await tools.TagsRemove(21, ["stale"], stamp, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("/api/v2/tickets/21/tags", request.Path);
        Assert.NotNull(request.Body);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("stale", body.RootElement.GetProperty("tags")[0].GetString());
        Assert.Equal(stamp, body.RootElement.GetProperty("updated_stamp").GetDateTimeOffset());
        Assert.Equal("true", body.RootElement.GetProperty("safe_update").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("vip", json.GetProperty("tags")[0].GetString());
    }

    [Fact]
    public async Task CommentMakePrivate_Puts_And_Acknowledges()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.CommentMakePrivate(31, 32, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/31/comments/32/make_private", request.Path);
        var acknowledgement = Assert.IsType<ZendeskWriteAcknowledgement>(result);
        Assert.Contains("comment 32", acknowledgement.Description);
        Assert.Equal(32, acknowledgement.Id);
    }

    [Fact]
    public async Task CommentAttachmentRedact_Puts_And_Returns_The_Attachment_Summary()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"attachment":{"id":33,"url":"https://unit-test.zendesk.com/api/v2/attachments/33.json",
             "file_name":"redacted.txt","content_url":"https://unit-test.zendesk.com/attachments/33",
             "content_type":"text/plain","size":0,"thumbnails":[{"id":34,"file_name":"thumb.png"}]}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.CommentAttachmentRedact(31, 32, 33, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/v2/tickets/31/comments/32/attachments/33/redact", request.Path);
        Assert.Null(request.Body);
        // The attachment summary shape: identity fields only — envelope, URLs and thumbnails are gone.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(33, json.GetProperty("id").GetInt64());
        Assert.Equal("redacted.txt", json.GetProperty("file_name").GetString());
        Assert.Equal("text/plain", json.GetProperty("content_type").GetString());
        Assert.Equal(0, json.GetProperty("size").GetInt64());
        Assert.False(json.TryGetProperty("attachment", out _));
        Assert.False(json.TryGetProperty("url", out _));
        Assert.False(json.TryGetProperty("content_url", out _));
        Assert.False(json.TryGetProperty("thumbnails", out _));
    }

    [Fact]
    public async Task Import_Posts_With_Archive_Immediately_And_Comment_Objects()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"ticket":{"id":51,"url":"https://unit-test.zendesk.com/api/v2/tickets/51.json",
             "subject":"Legacy ticket","status":"closed","created_at":"2020-01-02T03:04:05Z",
             "description":"Imported from the old helpdesk.","tags":["legacy"]}}
            """);
        var tools = CreateTools(harness);
        var createdAt = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var solvedAt = new DateTimeOffset(2020, 1, 3, 0, 0, 0, TimeSpan.Zero);
        var import = new ZendeskTicketImport
        {
            Subject = "Legacy ticket",
            Description = "Imported from the old helpdesk.",
            RequesterId = 55,
            Status = "closed",
            Tags = ["legacy"],
            Comments =
            [
                new ZendeskTicketImportComment
                {
                    AuthorId = 55, Body = "Original question", Public = true, CreatedAt = createdAt
                },
                new ZendeskTicketImportComment { AuthorId = 7, Body = "Internal note", Public = false }
            ],
            CreatedAt = createdAt,
            SolvedAt = solvedAt
        };

        var result = await tools.Import(import, true,
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/imports/tickets", request.Path);
        Assert.Contains("archive_immediately=true", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        var ticket = body.RootElement.GetProperty("ticket");
        Assert.Equal("Legacy ticket", ticket.GetProperty("subject").GetString());
        Assert.Equal("closed", ticket.GetProperty("status").GetString());
        Assert.Equal(createdAt, ticket.GetProperty("created_at").GetDateTimeOffset());
        Assert.Equal(solvedAt, ticket.GetProperty("solved_at").GetDateTimeOffset());
        // The spec models import comments as bare strings; the tool must send the documented objects.
        var comments = ticket.GetProperty("comments");
        Assert.Equal(2, comments.GetArrayLength());
        Assert.Equal(55, comments[0].GetProperty("author_id").GetInt64());
        Assert.Equal("Original question", comments[0].GetProperty("body").GetString());
        Assert.True(comments[0].GetProperty("public").GetBoolean());
        Assert.Equal(createdAt, comments[0].GetProperty("created_at").GetDateTimeOffset());
        Assert.False(comments[1].GetProperty("public").GetBoolean());
        Assert.False(comments[1].TryGetProperty("created_at", out _)); // unset fields are omitted
        // The minimal import confirmation: {id, subject, status, created_at} — nothing else echoed back.
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal(51, json.GetProperty("id").GetInt64());
        Assert.Equal("Legacy ticket", json.GetProperty("subject").GetString());
        Assert.Equal("closed", json.GetProperty("status").GetString());
        Assert.Equal("2020-01-02T03:04:05Z", json.GetProperty("created_at").GetString());
        Assert.False(json.TryGetProperty("ticket", out _));
        Assert.False(json.TryGetProperty("description", out _));
        Assert.False(json.TryGetProperty("tags", out _));
    }

    [Fact]
    public async Task Import_Omits_Archive_Immediately_When_False()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"ticket":{"id":52}}""");
        var tools = CreateTools(harness);

        await tools.Import(new ZendeskTicketImport { Subject = "Legacy" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("archive_immediately", harness.Request.Query);
    }

    [Fact]
    public async Task ImportMany_Posts_To_Create_Many_As_Job()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"job_status":{"id":"job-10","status":"queued"}}""");
        var tools = CreateTools(harness);
        var imports = new[] { new ZendeskTicketImport { Subject = "Legacy A", Status = "closed" } };

        var result = await tools.ImportMany(imports, true,
            TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v2/imports/tickets/create_many", request.Path);
        Assert.Contains("archive_immediately=true", request.Query);
        using var body = JsonDocument.Parse(request.Body!);
        var tickets = body.RootElement.GetProperty("tickets");
        Assert.Equal(1, tickets.GetArrayLength());
        Assert.Equal("Legacy A", tickets[0].GetProperty("subject").GetString());
        Assert.Equal("closed", tickets[0].GetProperty("status").GetString());
        var json = Assert.IsType<JsonElement>(result);
        Assert.Equal("job-10", json.GetProperty("id").GetString());
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ImportMany_Rejects_More_Than_100_Tickets()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);
        var imports = Enumerable.Range(0, 101).Select(i => new ZendeskTicketImport { Subject = $"L{i}" })
            .ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ImportMany(imports, cancellationToken: TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept 1–100 items per call", exception.Message);
        Assert.Equal("tickets", exception.ParamName);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_Single_Setter_Returns_DryRunResult_And_Sends_Nothing()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);

        var result = await tools.StatusSet(42, "solved", cancellationToken: TestContext.Current.CancellationToken);

        // Single-setter dry-runs echo exactly the fields the setter would send — that echo is the verification value.
        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("set ticket 42 status to 'solved'", dryRun.Description);
        var echo = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal(42L, (long?)echo["id"]);
        Assert.Equal("solved", (string?)echo["status"]);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_Bulk_Setter_Returns_A_Per_Id_Digest_And_Sends_Nothing()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);

        var result = await tools.StatusSetMany([1, 2], "solved", TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        Assert.False(dryRun.Executed);
        Assert.Contains("set 2 tickets to status 'solved'", dryRun.Description);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("update", (string?)digest["action"]);
        Assert.Equal("tickets", (string?)digest["target"]);
        Assert.Equal(2, (int?)digest["count"]);
        // The shared change expands to per-id rows: which record, which fields — values are not echoed.
        var items = Assert.IsType<JsonArray>(digest["items"]);
        Assert.Equal(1L, (long?)items[0]!["id"]);
        Assert.Equal(2L, (long?)items[1]!["id"]);
        Assert.Contains("status", items[0]!["fields"]!.AsArray().Select(field => (string?)field));
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_CreateMany_Digests_Items_With_Truncated_Identity_Values()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);
        var writes = new[]
        {
            new ZendeskTicketWrite { Subject = new string('s', 150), Status = "new" },
            new ZendeskTicketWrite { Subject = "B" }
        };

        var result = await tools.CreateMany(writes, TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("create", (string?)digest["action"]);
        var items = Assert.IsType<JsonArray>(digest["items"]);
        // Long identity values are truncated; the changed FIELD NAMES (not values) are listed per item.
        var subject = (string?)items[0]!["subject"];
        Assert.NotNull(subject);
        Assert.True(subject!.Length < 150);
        Assert.EndsWith("…", subject);
        Assert.Contains("status", items[0]!["fields"]!.AsArray().Select(field => (string?)field));
        Assert.Equal("B", (string?)items[1]!["subject"]);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_DeleteMany_Digests_The_Ids()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);

        var result = await tools.DeleteMany([7, 8], TestContext.Current.CancellationToken);

        var dryRun = Assert.IsType<ZendeskDryRunResult>(result);
        var digest = Assert.IsType<JsonObject>(dryRun.Request);
        Assert.Equal("delete", (string?)digest["action"]);
        Assert.Equal("tickets", (string?)digest["target"]);
        Assert.Equal(2, (int?)digest["count"]);
        // Primitive (id-list) bulk items digest to {index, id} rows.
        var items = Assert.IsType<JsonArray>(digest["items"]);
        Assert.Equal(0, (int?)items[0]!["index"]);
        Assert.Equal(7L, (long?)items[0]!["id"]);
        Assert.Equal(8L, (long?)items[1]!["id"]);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DryRun_Bulk_Still_Validates_The_Item_Count()
    {
        // A dry run must catch the same contract violation the live call would (Zendesk's 100-item cap).
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.DryRun);
        var ids = Enumerable.Range(1, 101).Select(i => (long)i).ToArray();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.DeleteMany(ids, TestContext.Current.CancellationToken));

        Assert.StartsWith("Zendesk bulk operations accept 1–100 items per call", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ReadOnly_Rejects_And_Sends_Nothing()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness, McpExecutionMode.ReadOnly);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Delete(7, TestContext.Current.CancellationToken));

        Assert.Contains("read-only", exception.Message);
        Assert.Empty(harness.Requests);
    }
}