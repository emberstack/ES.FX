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
}