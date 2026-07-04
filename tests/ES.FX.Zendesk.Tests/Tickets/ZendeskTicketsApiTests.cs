using System.Net;
using System.Text;
using ES.FX.Zendesk.Abstractions.Models;
using ES.FX.Zendesk.Tests.Testing;
using ES.FX.Zendesk.Tickets;
using Microsoft.Extensions.Logging.Abstractions;

namespace ES.FX.Zendesk.Tests.Tickets;

public class ZendeskTicketsApiTests
{
    private static ZendeskTicketsApi CreateApi(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://acme.zendesk.com/api/v2/") },
            NullLogger<ZendeskTicketsApi>.Instance);

    [Fact]
    public async Task GetByIdAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket": { "id": 99, "subject": "Printer on fire", "status": "open", "priority": "high" } }""");
        var api = CreateApi(stub);

        var ticket = await api.GetByIdAsync(99, TestContext.Current.CancellationToken);

        Assert.Equal(99, ticket.Id);
        Assert.Equal("open", ticket.Status);
        Assert.Equal("high", ticket.Priority);
        Assert.Equal("https://acme.zendesk.com/api/v2/tickets/99.json", stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task SearchAsync_Scopes_To_Tickets_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "results": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""");
        var api = CreateApi(stub);

        var results = await api.SearchAsync("status:open", "created_at", "desc",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results.Results.Count);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/search.json", uri.AbsolutePath);
        Assert.Contains("query=type%3Aticket%20status%3Aopen", uri.Query);
        Assert.Contains("sort_by=created_at", uri.Query);
        Assert.Contains("sort_order=desc", uri.Query);
    }

    [Fact]
    public async Task SearchAsync_Does_Not_Double_Scope_When_Type_Selector_Already_Present()
    {
        var stub = new StubHttpMessageHandler("""{ "results": [], "count": 0 }""");
        var api = CreateApi(stub);

        await api.SearchAsync("type:ticket status:closed", cancellationToken: TestContext.Current.CancellationToken);

        var query = stub.LastRequest!.RequestUri!.Query;
        Assert.Contains("query=type%3Aticket%20status%3Aclosed", query);
        Assert.DoesNotContain("type%3Aticket%20type%3Aticket", query);
    }

    [Theory]
    [InlineData("ticket_type:incident")]
    [InlineData("support_type:agent")]
    [InlineData("content-type: json")]
    public async Task SearchAsync_Still_Scopes_When_Type_Substring_Is_Not_A_Selector(string query)
    {
        var stub = new StubHttpMessageHandler("""{ "results": [], "count": 0 }""");
        var api = CreateApi(stub);

        await api.SearchAsync(query, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("query=type%3Aticket%20", stub.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task GetCommentsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "public": true, "plain_body": "hello" }, { "id": 2, "public": false, "plain_body": "note" } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result =
            await api.GetCommentsAsync(99, perPage: 50, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Comments.Count);
        Assert.True(result.Comments[0].Public);
        Assert.False(result.Comments[1].Public);
        Assert.Equal("/api/v2/tickets/99/comments.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("per_page=50", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetCommentsAsync_Plain_BodyFormat_Drops_Rich_Body_By_Default()
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "public": true, "plain_body": "hello", "body": "<b>hello</b>" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetCommentsAsync(99, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello", result.Comments[0].PlainBody);
        Assert.Null(result.Comments[0].Body); // default "plain" drops the rich body to halve tokens
    }

    [Fact]
    public async Task GetCommentsAsync_Both_BodyFormat_Keeps_Both_Bodies()
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "public": true, "plain_body": "hello", "body": "<b>hello</b>" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetCommentsAsync(99, bodyFormat: "both",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello", result.Comments[0].PlainBody);
        Assert.Equal("<b>hello</b>", result.Comments[0].Body);
    }

    [Fact]
    public async Task GetCommentsAsync_Rich_BodyFormat_Keeps_Rich_Drops_Plain()
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "public": true, "plain_body": "hello", "body": "<b>hello</b>" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetCommentsAsync(99, bodyFormat: "rich",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("<b>hello</b>", result.Comments[0].Body);
        Assert.Null(result.Comments[0].PlainBody);
    }

    [Theory]
    [InlineData("html")] // unrecognized -> plain fallback
    [InlineData(" PLAIN ")] // trim + case-insensitive
    public async Task GetCommentsAsync_Unrecognized_Or_Cased_BodyFormat_Falls_Back_To_Plain(string bodyFormat)
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "public": true, "plain_body": "hello", "body": "<b>hello</b>" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetCommentsAsync(99, bodyFormat: bodyFormat,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("hello", result.Comments[0].PlainBody);
        Assert.Null(result.Comments[0].Body);
    }

    [Fact]
    public async Task GetByIdAsync_Parses_Enriched_Ticket_Fields()
    {
        var stub = new StubHttpMessageHandler(
            """
            { "ticket": { "id": 99, "custom_status_id": 123, "problem_id": 500, "has_incidents": true,
                          "collaborator_ids": [ 1, 2 ], "email_cc_ids": [ 3 ], "follower_ids": [ 4 ],
                          "via": { "channel": "email" },
                          "satisfaction_rating": { "id": 9, "score": "good", "comment": "thanks" },
                          "custom_fields": [ { "id": 10, "value": "gold" }, { "id": 11, "value": 42 },
                                             { "id": 12, "value": [ "a", "b" ] }, { "id": 13, "value": null } ] } }
            """);
        var api = CreateApi(stub);

        var ticket = await api.GetByIdAsync(99, TestContext.Current.CancellationToken);

        Assert.Equal(123, ticket.CustomStatusId);
        Assert.Equal(500, ticket.ProblemId);
        Assert.True(ticket.HasIncidents);
        Assert.Equal(new long[] { 1, 2 }, ticket.CollaboratorIds!);
        Assert.Equal(new long[] { 3 }, ticket.EmailCcIds!);
        Assert.Equal(new long[] { 4 }, ticket.FollowerIds!);
        Assert.Equal("email", ticket.Via?.Channel);
        Assert.Equal("good", ticket.SatisfactionRating?.Score);
        Assert.Equal("thanks", ticket.SatisfactionRating?.Comment);
        Assert.NotNull(ticket.CustomFields);
        Assert.Equal(4, ticket.CustomFields.Count);
        Assert.Equal("gold", ticket.CustomFields[0].Value?.GetString()); // string value
        Assert.Equal(42, ticket.CustomFields[1].Value?.GetInt32()); // numeric value
        Assert.Equal(2, ticket.CustomFields[2].Value?.GetArrayLength()); // array value
    }

    [Fact]
    public async Task SearchAsync_Sideloads_With_Nested_Include_Syntax_And_Parses_Siblings()
    {
        var stub = new StubHttpMessageHandler(
            """{ "results": [ { "id": 1, "assignee_id": 7, "group_id": 5 } ], "count": 1, "users": [ { "id": 7, "name": "Agent" } ], "groups": [ { "id": 5, "name": "Tier 2" } ] }""");
        var api = CreateApi(stub);

        var results = await api.SearchAsync("status:open", include: ["users", "groups"],
            cancellationToken: TestContext.Current.CancellationToken);

        // Search uses the nested include=tickets(users,groups) syntax (encoded), not a flat list.
        Assert.Contains("include=tickets%28users%2Cgroups%29", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Agent", results.Users?[0].Name);
        Assert.Equal("Tier 2", results.Groups?[0].Name);
    }

    [Fact]
    public async Task GetIncidentsAsync_Requests_Correct_Path()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 2 } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetIncidentsAsync(500, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Tickets);
        Assert.Equal("/api/v2/tickets/500/incidents.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetSideConversationsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "side_conversations": [ { "id": "abc", "subject": "Vendor", "state": "open" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetSideConversationsAsync(99, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.SideConversations);
        Assert.Equal("Vendor", result.SideConversations[0].Subject);
        Assert.Equal("/api/v2/tickets/99/side_conversations.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetMetricEventsAsync_Uses_The_Incremental_Export_And_Parses()
    {
        // Zendesk has NO per-ticket metric-events endpoint (tickets/{id}/metric_events answers 200 with an
        // empty body — live-verified). The incremental export is the only real source.
        var stub = new StubHttpMessageHandler(
            """{ "ticket_metric_events": [ { "id": 1, "ticket_id": 99, "metric": "reply_time", "type": "breach" } ], "count": 1, "end_time": 1700000000, "end_of_stream": true }""");
        var api = CreateApi(stub);

        var result = await api.GetMetricEventsAsync(1690000000, TestContext.Current.CancellationToken);

        Assert.Single(result.MetricEvents);
        Assert.Equal("reply_time", result.MetricEvents[0].Metric);
        Assert.Equal("breach", result.MetricEvents[0].Type);
        Assert.Equal(1700000000, result.EndTime);
        Assert.True(result.EndOfStream);
        Assert.Equal("/api/v2/incremental/ticket_metric_events.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("start_time=1690000000", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetAuditsAsync_Requests_Correct_Path_And_Parses_Events()
    {
        var stub = new StubHttpMessageHandler(
            """{ "audits": [ { "id": 10, "events": [ { "id": 1, "type": "Change", "field_name": "status", "value": "open" } ] } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetAuditsAsync(99, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Audits);
        Assert.Equal("status", result.Audits[0].Events[0].FieldName);
        Assert.Equal("/api/v2/tickets/99/audits.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetMetricsAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket_metric": { "id": 5, "ticket_id": 99, "reopens": 2, "replies": 4, "reply_time_in_minutes": { "business": 10, "calendar": 20 } } }""");
        var api = CreateApi(stub);

        var metric = await api.GetMetricsAsync(99, TestContext.Current.CancellationToken);

        Assert.Equal(2, metric.Reopens);
        Assert.Equal(10, metric.ReplyTimeInMinutes?.Business);
        Assert.Equal("https://acme.zendesk.com/api/v2/tickets/99/metrics.json",
            stub.LastRequest?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ListAsync_Uses_Cursor_Params_And_Parses_Meta()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1 } ], "meta": { "has_more": true, "after_cursor": "xyz" } }""");
        var api = CreateApi(stub);

        var result = await api.ListAsync(100, "prev==", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Tickets);
        Assert.True(result.Meta?.HasMore);
        Assert.Equal("xyz", result.Meta?.AfterCursor);

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/tickets.json", uri.AbsolutePath);
        Assert.Contains("page[size]=100", uri.Query);
        Assert.Contains("page[after]=prev%3D%3D", uri.Query);
    }

    [Fact]
    public async Task GetManyAsync_Requests_ShowMany_With_Comma_Joined_Ids()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [ { "id": 1 }, { "id": 2 } ], "count": 2 }""");
        var api = CreateApi(stub);

        var result = await api.GetManyAsync([1, 2], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Tickets.Count);
        Assert.Equal("/api/v2/tickets/show_many.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetManyAsync_Chunks_Requests_Over_100_Ids_And_Merges()
    {
        var requests = new List<string>();
        var responder = new CountingHandler(request =>
        {
            requests.Add(request.RequestUri!.Query);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "tickets": [ { "id": 1 } ], "count": 1 }""",
                    Encoding.UTF8, "application/json")
            };
        });
        var api = CreateApi(responder);
        var ids = Enumerable.Range(1, 250).Select(i => (long)i).ToArray();

        var result = await api.GetManyAsync(ids, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, responder.Calls); // 100 + 100 + 50
        Assert.Equal(3, result.Tickets.Count); // merged across chunks
        Assert.Contains("ids=1%2C", requests[0]); // chunk 1 starts at id 1...
        Assert.DoesNotContain("101", requests[0]); // ...and stops at 100
        Assert.Contains("ids=101%2C", requests[1]); // chunk 2 starts at id 101
        Assert.Contains("ids=201%2C", requests[2]); // chunk 3 starts at id 201
    }

    [Fact]
    public async Task GetManyAsync_Empty_Ids_Returns_Empty_Without_A_Call()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [] }""");
        var api = CreateApi(stub);

        var result = await api.GetManyAsync([], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Tickets);
        Assert.Null(stub.LastRequest);
    }

    [Fact]
    public async Task CountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler(
            """{ "count": { "value": 102773, "refreshed_at": "2026-07-01T00:00:00Z" } }""");
        var api = CreateApi(stub);

        var count = await api.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(102773, count.Value);
        Assert.NotNull(count.RefreshedAt);
        Assert.Equal("/api/v2/tickets/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetByExternalIdAsync_Filters_By_External_Id()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1, "external_id": "crm-42" } ], "count": 1 }""");
        var api = CreateApi(stub);

        var result = await api.GetByExternalIdAsync("crm-42", TestContext.Current.CancellationToken);

        Assert.Single(result.Tickets);
        Assert.Equal("/api/v2/tickets.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("external_id=crm-42", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetCollaboratorsAsync_Parses_Users_Envelope()
    {
        // Collaborators are returned in a "users" envelope, not "collaborators".
        var stub = new StubHttpMessageHandler("""{ "users": [ { "id": 3, "name": "CC'd Agent" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetCollaboratorsAsync(99, TestContext.Current.CancellationToken);

        Assert.Single(result.Users);
        Assert.Equal("CC'd Agent", result.Users[0].Name);
        Assert.Equal("/api/v2/tickets/99/collaborators.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetCommentsCountAsync_Requests_Correct_Path_And_Parses()
    {
        var stub = new StubHttpMessageHandler("""{ "count": { "value": 17 } }""");
        var api = CreateApi(stub);

        var count = await api.GetCommentsCountAsync(99, TestContext.Current.CancellationToken);

        Assert.Equal(17, count.Value);
        Assert.Equal("/api/v2/tickets/99/comments/count.json", stub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetIncrementalAsync_Initial_Call_Uses_StartTime()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1 } ], "after_cursor": "cur1", "end_of_stream": false }""");
        var api = CreateApi(stub);

        var result = await api.GetIncrementalAsync(1690000000,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result.Tickets);
        Assert.Equal("cur1", result.AfterCursor);
        Assert.False(result.EndOfStream);
        Assert.Equal("/api/v2/incremental/tickets/cursor.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("start_time=1690000000", stub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task GetIncrementalAsync_Continuation_Uses_Cursor()
    {
        var stub = new StubHttpMessageHandler("""{ "tickets": [], "end_of_stream": true }""");
        var api = CreateApi(stub);

        var result = await api.GetIncrementalAsync(cursor: "cur1",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.EndOfStream);
        Assert.Contains("cursor=cur1", stub.LastRequest!.RequestUri!.Query);
        Assert.DoesNotContain("start_time", stub.LastRequest.RequestUri.Query);
    }

    [Theory]
    [InlineData(true, true)] // both supplied
    [InlineData(false, false)] // neither supplied
    public async Task GetIncrementalAsync_Rejects_Ambiguous_Arguments(bool withStartTime, bool withCursor)
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => api.GetIncrementalAsync(
            withStartTime ? 1690000000 : null, withCursor ? "cur1" : null,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_Posts_Ticket_Envelope_And_Omits_Unset_Fields()
    {
        var stub = new StubHttpMessageHandler("""{ "ticket": { "id": 900, "subject": "Printer on fire" } }""");
        var api = CreateApi(stub);

        var ticket = await api.CreateAsync(new ZendeskTicketWrite
        {
            Subject = "Printer on fire",
            Comment = new ZendeskTicketCommentWrite { Body = "It is very much on fire.", Public = true },
            Priority = "high"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(900, ticket.Id);
        Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"ticket\":{", stub.LastRequestBody);
        Assert.Contains("\"subject\":\"Printer on fire\"", stub.LastRequestBody);
        Assert.Contains("\"comment\":{\"body\":\"It is very much on fire.\",\"public\":true}", stub.LastRequestBody);
        Assert.DoesNotContain("\"status\"", stub.LastRequestBody); // unset fields are omitted
        Assert.DoesNotContain("\"id\"", stub.LastRequestBody); // batch-only Id never leaks into creates
    }

    [Fact]
    public async Task UpdateAsync_Puts_Ticket_Envelope_And_Returns_Ticket_And_Audit()
    {
        var stub = new StubHttpMessageHandler(
            """{ "ticket": { "id": 99, "status": "solved" }, "audit": { "id": 1010, "events": [] } }""");
        var api = CreateApi(stub);

        var result = await api.UpdateAsync(99,
            new ZendeskTicketWrite { Status = "solved", SafeUpdate = true, UpdatedStamp = DateTimeOffset.UnixEpoch },
            TestContext.Current.CancellationToken);

        Assert.Equal("solved", result.Ticket?.Status);
        Assert.Equal(1010, result.Audit?.Id);
        Assert.Equal(HttpMethod.Put, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/99.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"safe_update\":true", stub.LastRequestBody);
        Assert.Contains("\"updated_stamp\":", stub.LastRequestBody);
    }

    [Fact]
    public async Task UpdateManyAsync_Bulk_Uses_Ids_Query_And_Batch_Requires_Ids()
    {
        var job = """{ "job_status": { "id": "j1", "status": "queued" } }""";
        var bulkStub = new StubHttpMessageHandler(job);
        var bulk = await CreateApi(bulkStub).UpdateManyAsync([1, 2],
            new ZendeskTicketWrite { AdditionalTags = ["vip"] }, TestContext.Current.CancellationToken);
        Assert.Equal("queued", bulk.Status);
        Assert.Equal(HttpMethod.Put, bulkStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/update_many.json", bulkStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", bulkStub.LastRequest.RequestUri.Query);
        Assert.Contains("\"ticket\":{\"additional_tags\":[\"vip\"]}", bulkStub.LastRequestBody);

        var batchStub = new StubHttpMessageHandler(job);
        await CreateApi(batchStub).UpdateManyAsync(
            [new ZendeskTicketWrite { Id = 1, Status = "open" }], TestContext.Current.CancellationToken);
        Assert.Equal("", batchStub.LastRequest!.RequestUri!.Query); // batch form has no ids query
        Assert.Contains("\"tickets\":[{\"id\":1,\"status\":\"open\"}]", batchStub.LastRequestBody);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateApi(new StubHttpMessageHandler(job))
            .UpdateManyAsync([new ZendeskTicketWrite { Status = "open" }], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_Sends_Delete_And_DeleteManyAsync_Returns_Job()
    {
        var deleteStub = new StubHttpMessageHandler("");
        await CreateApi(deleteStub).DeleteAsync(99, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, deleteStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/99.json", deleteStub.LastRequest.RequestUri!.AbsolutePath);

        var manyStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j2", "status": "working" } }""");
        var job = await CreateApi(manyStub).DeleteManyAsync([1, 2, 3], TestContext.Current.CancellationToken);
        Assert.Equal("working", job.Status);
        Assert.Equal("/api/v2/tickets/destroy_many.json", manyStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2%2C3", manyStub.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task MergeAsync_Posts_A_Bare_Payload_Without_Ticket_Envelope()
    {
        var stub = new StubHttpMessageHandler("""{ "job_status": { "id": "j3", "status": "queued" } }""");
        var api = CreateApi(stub);

        await api.MergeAsync(42, [1, 2], "Merged here.",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, stub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/42/merge.json", stub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"ids\":[1,2]", stub.LastRequestBody);
        Assert.Contains("\"target_comment\":\"Merged here.\"", stub.LastRequestBody);
        Assert.DoesNotContain("\"ticket\"", stub.LastRequestBody); // bare object — no envelope
        Assert.DoesNotContain("\"source_comment\"", stub.LastRequestBody); // unset — omitted
    }

    [Fact]
    public async Task Spam_Restore_And_Permanent_Delete_Use_Documented_Paths_And_Shapes()
    {
        var spamStub = new StubHttpMessageHandler("");
        await CreateApi(spamStub).MarkAsSpamAsync(99, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, spamStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/99/mark_as_spam.json", spamStub.LastRequest.RequestUri!.AbsolutePath);

        var restoreStub = new StubHttpMessageHandler("");
        await CreateApi(restoreStub).RestoreDeletedAsync(99, TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/deleted_tickets/99/restore.json", restoreStub.LastRequest!.RequestUri!.AbsolutePath);

        // QUIRK: the single permanent delete is an async job.
        var purgeStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j4", "status": "queued" } }""");
        var job = await CreateApi(purgeStub).DeletePermanentlyAsync(99, TestContext.Current.CancellationToken);
        Assert.Equal("queued", job.Status);
        Assert.Equal(HttpMethod.Delete, purgeStub.LastRequest!.Method);
        Assert.Equal("/api/v2/deleted_tickets/99.json", purgeStub.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Tag_Operations_Use_Post_Put_Delete_With_Tags_Envelope()
    {
        var setStub = new StubHttpMessageHandler("""{ "tags": [ "vip", "billing" ] }""");
        var set = await CreateApi(setStub).SetTagsAsync(99, ["vip", "billing"],
            TestContext.Current.CancellationToken);
        Assert.Equal(["vip", "billing"], set.Tags);
        Assert.Equal(HttpMethod.Post, setStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/99/tags.json", setStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"tags\":[\"vip\",\"billing\"]", setStub.LastRequestBody);

        var addStub = new StubHttpMessageHandler("""{ "tags": [ "vip" ] }""");
        await CreateApi(addStub).AddTagsAsync(99, ["vip"], DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, addStub.LastRequest!.Method);
        Assert.Contains("\"updated_stamp\":", addStub.LastRequestBody);
        Assert.Contains("\"safe_update\":\"true\"", addStub.LastRequestBody); // docs pass the string form

        // A DELETE with a JSON body — the documented removal shape.
        var removeStub = new StubHttpMessageHandler("""{ "tags": [] }""");
        await CreateApi(removeStub).RemoveTagsAsync(99, ["vip"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Delete, removeStub.LastRequest!.Method);
        Assert.Contains("\"tags\":[\"vip\"]", removeStub.LastRequestBody);
        Assert.DoesNotContain("safe_update", removeStub.LastRequestBody); // no stamp — omitted
    }

    [Fact]
    public async Task Comment_Privacy_And_Attachment_Redaction_Use_Documented_Paths()
    {
        var privateStub = new StubHttpMessageHandler("");
        await CreateApi(privateStub).MakeCommentPrivateAsync(99, 7, TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, privateStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/99/comments/7/make_private.json",
            privateStub.LastRequest.RequestUri!.AbsolutePath);

        var redactStub = new StubHttpMessageHandler(
            """{ "attachment": { "id": 5, "file_name": "redacted.txt" } }""");
        var attachment = await CreateApi(redactStub).RedactCommentAttachmentAsync(99, 7, 5,
            TestContext.Current.CancellationToken);
        Assert.Equal("redacted.txt", attachment.FileName);
        Assert.Equal("/api/v2/tickets/99/comments/7/attachments/5/redact.json",
            redactStub.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ImportAsync_Posts_To_Imports_With_Optional_Archive_Flag()
    {
        var stub = new StubHttpMessageHandler("""{ "ticket": { "id": 800, "status": "closed" } }""");
        var api = CreateApi(stub);

        var ticket = await api.ImportAsync(new ZendeskTicketImport
        {
            Subject = "Old ticket",
            Status = "closed",
            Comments = [new ZendeskTicketImportComment { AuthorId = 1, Body = "historical", Public = true }]
        }, true, TestContext.Current.CancellationToken);

        Assert.Equal(800, ticket.Id);
        Assert.Equal("/api/v2/imports/tickets.json", stub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("archive_immediately=true", stub.LastRequest.RequestUri.Query);
        Assert.Contains("\"comments\":[{\"author_id\":1", stub.LastRequestBody);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Bulk_Write_Operations_Reject_Invalid_Id_Counts(int count)
    {
        var api = CreateApi(new StubHttpMessageHandler("{}"));
        var ids = Enumerable.Range(1, count).Select(i => (long)i).ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.DeleteManyAsync(ids, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.MarkManyAsSpamAsync(ids, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetManyAsync_Sideloads_And_Parses_Siblings_And_CommentCount()
    {
        var stub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1, "assignee_id": 7, "comment_count": 12 } ], "users": [ { "id": 7, "name": "Agent" } ], "groups": [ { "id": 5, "name": "Tier 2" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetManyAsync([1], ["users", "groups", "comment_count"],
            TestContext.Current.CancellationToken);

        Assert.Contains("include=users%2Cgroups%2Ccomment_count", stub.LastRequest!.RequestUri!.Query);
        Assert.Equal(12, result.Tickets[0].CommentCount);
        Assert.Equal("Agent", result.Users?[0].Name);
        Assert.Equal("Tier 2", result.Groups?[0].Name);
    }

    [Fact]
    public async Task GetManyAsync_Chunked_Merges_And_Dedupes_Sideloads()
    {
        // Both chunks return the SAME sideloaded user — the merged result must carry it once.
        var responder = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{ "tickets": [ { "id": 1 } ], "users": [ { "id": 7, "name": "Agent" } ] }""",
                Encoding.UTF8, "application/json")
        });
        var api = CreateApi(responder);
        var ids = Enumerable.Range(1, 150).Select(i => (long)i).ToArray();

        var result = await api.GetManyAsync(ids, ["users"], TestContext.Current.CancellationToken);

        Assert.Equal(2, responder.Calls);
        Assert.Equal(2, result.Tickets.Count);
        Assert.Single(result.Users!); // deduplicated by id across chunks
    }

    [Fact]
    public async Task GetCommentsAsync_Sideloads_Users_And_Preserves_Them_Through_Body_Projection()
    {
        var stub = new StubHttpMessageHandler(
            """{ "comments": [ { "id": 1, "plain_body": "hi", "body": "<b>hi</b>", "author_id": 7 } ], "count": 1, "users": [ { "id": 7, "name": "Requester" } ] }""");
        var api = CreateApi(stub);

        var result = await api.GetCommentsAsync(99, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("include=users", stub.LastRequest!.RequestUri!.Query);
        // The default "plain" body projection must not drop the sideloaded authors.
        Assert.Null(result.Comments[0].Body);
        Assert.Equal("Requester", result.Users?[0].Name);
    }

    [Fact]
    public async Task CreateManyAsync_And_ImportManyAsync_Post_Tickets_Arrays_As_Jobs()
    {
        var job = """{ "job_status": { "id": "j1", "status": "queued" } }""";
        var createStub = new StubHttpMessageHandler(job);
        var created = await CreateApi(createStub).CreateManyAsync(
            [new ZendeskTicketWrite { Subject = "A", Comment = new ZendeskTicketCommentWrite { Body = "a" } }],
            TestContext.Current.CancellationToken);
        Assert.Equal("queued", created.Status);
        Assert.Equal(HttpMethod.Post, createStub.LastRequest!.Method);
        Assert.Equal("/api/v2/tickets/create_many.json", createStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"tickets\":[{\"subject\":\"A\"", createStub.LastRequestBody);

        var importStub = new StubHttpMessageHandler(job);
        await CreateApi(importStub).ImportManyAsync(
            [new ZendeskTicketImport { Subject = "Old", Status = "closed" }], true,
            TestContext.Current.CancellationToken);
        Assert.Equal("/api/v2/imports/tickets/create_many.json", importStub.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("archive_immediately=true", importStub.LastRequest.RequestUri.Query);
        Assert.Contains("\"tickets\":[{\"subject\":\"Old\"", importStub.LastRequestBody);
    }

    [Fact]
    public async Task RestoreManyDeleted_And_DeleteManyPermanently_Use_DeletedTickets_Paths()
    {
        // restore_many is synchronous with an EMPTY response body — not a job.
        var restoreStub = new StubHttpMessageHandler("");
        await CreateApi(restoreStub).RestoreManyDeletedAsync([1, 2], TestContext.Current.CancellationToken);
        Assert.Equal(HttpMethod.Put, restoreStub.LastRequest!.Method);
        Assert.Equal("/api/v2/deleted_tickets/restore_many.json", restoreStub.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("ids=1%2C2", restoreStub.LastRequest.RequestUri.Query);

        var purgeStub = new StubHttpMessageHandler("""{ "job_status": { "id": "j5", "status": "queued" } }""");
        var job = await CreateApi(purgeStub).DeleteManyPermanentlyAsync([1, 2],
            TestContext.Current.CancellationToken);
        Assert.Equal("queued", job.Status);
        Assert.Equal(HttpMethod.Delete, purgeStub.LastRequest!.Method);
        Assert.Equal("/api/v2/deleted_tickets/destroy_many.json", purgeStub.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task MergeAsync_Accepts_More_Than_100_Source_Ids()
    {
        // Merge is NOT a 100-capped bulk endpoint — a large id list must go through.
        var stub = new StubHttpMessageHandler("""{ "job_status": { "id": "j6", "status": "queued" } }""");
        var ids = Enumerable.Range(1, 150).Select(i => (long)i).ToArray();

        var job = await CreateApi(stub).MergeAsync(42, ids,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("queued", job.Status);
        Assert.Contains("\"ids\":[1,", stub.LastRequestBody);
        Assert.Contains("150]", stub.LastRequestBody);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateApi(stub)
            .MergeAsync(42, [], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAuditsAsync_And_GetIncrementalAsync_Pass_Include_And_Parse_Siblings()
    {
        var auditsStub = new StubHttpMessageHandler(
            """{ "audits": [ { "id": 10, "events": [] } ], "users": [ { "id": 7, "name": "Actor" } ] }""");
        var audits = await CreateApi(auditsStub).GetAuditsAsync(99, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=users", auditsStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Actor", audits.Users?[0].Name);

        var incrementalStub = new StubHttpMessageHandler(
            """{ "tickets": [ { "id": 1 } ], "end_of_stream": true, "organizations": [ { "id": 9, "name": "Acme" } ] }""");
        var incremental = await CreateApi(incrementalStub).GetIncrementalAsync(1690000000,
            include: ["organizations"], cancellationToken: TestContext.Current.CancellationToken);
        Assert.Contains("include=organizations", incrementalStub.LastRequest!.RequestUri!.Query);
        Assert.Equal("Acme", incremental.Organizations?[0].Name);
    }
}