using System.Net;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

public class ZendeskTicketToolsTests
{
    private static ZendeskTicketTools CreateTools(ZendeskToolHarness harness, bool withResponseGuard = false,
        McpOptions? options = null) =>
        new(harness.CreateSupportClient(), harness.CreateAdapter(withResponseGuard),
            new StaticOptionsMonitor<McpOptions>(options ?? new McpOptions()));

    [Fact]
    public async Task Read_Requests_The_Ticket_And_Returns_The_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"ticket":{"id":99,"url":"https://unit-test.zendesk.com/api/v2/tickets/99.json","subject":"Printer on fire",
             "status":"open","priority":"high","organization_id":null,"created_at":"2026-01-01T00:00:00Z",
             "tags":["vip"],"requester_id":5,"has_incidents":true}}
            """);
        var tools = CreateTools(harness);

        var ticket = await tools.Read(99, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99", request.Path);
        Assert.Equal(string.Empty, request.Query);
        // Raw passthrough must keep the read-only fields the generated TicketObject drops on re-serialization.
        Assert.Equal(99, ticket.GetProperty("id").GetInt64());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.Equal("vip", ticket.GetProperty("tags")[0].GetString());
        Assert.True(ticket.GetProperty("has_incidents").GetBoolean());
        // ...while the full view drops API self-links and null-valued fields (absent = null/empty).
        Assert.False(ticket.TryGetProperty("url", out _));
        Assert.False(ticket.TryGetProperty("organization_id", out _));
    }

    [Fact]
    public async Task Read_Throws_When_The_Ticket_Is_Missing()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(99, TestContext.Current.CancellationToken));

        Assert.Contains("'99'", exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Comments_Requests_Pagination_And_Defaults_To_Plain_Bodies()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"comments":[{"id":1,"type":"Comment","author_id":5,"body":"<p>hi</p>","html_body":"<p>hi</p>",
             "plain_body":"hi","public":true,"audit_id":10,"created_at":"2026-01-01T00:00:00Z",
             "attachments":[{"id":77,"file_name":"log.txt","content_url":"https://unit-test.zendesk.com/a/77",
             "content_type":"text/plain","size":123,"inline":false}],"via":{"channel":"web"}}],
             "count":3,"next_page":"https://unit-test.zendesk.com/api/v2/tickets/99/comments.json?page=2"}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, 1, 25, "plain", null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/comments", request.Path);
        Assert.Equal("?page=1&per_page=25", request.Query);
        // The uniform list envelope: metadata first, comment rows in 'items'.
        Assert.Equal("full", result.GetProperty("detail").GetString());
        Assert.Equal(3, result.GetProperty("count").GetInt32());
        // Zendesk's next_page URL becomes a computed page NUMBER ((request page 1) + 1) with has_more.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal(2, result.GetProperty("next_page").GetInt32());
        var comment = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal(1, comment.GetProperty("id").GetInt64());
        Assert.Equal(5, comment.GetProperty("author_id").GetInt64());
        // The default 'plain' projection drops the rich body and keeps the plain one.
        Assert.False(comment.TryGetProperty("body", out _));
        Assert.Equal("hi", comment.GetProperty("plain_body").GetString());
        Assert.True(comment.GetProperty("public").GetBoolean());
        Assert.Equal(10, comment.GetProperty("audit_id").GetInt64());
        var attachment = Assert.Single(comment.GetProperty("attachments").EnumerateArray());
        Assert.Equal(77, attachment.GetProperty("id").GetInt64());
        Assert.Equal("log.txt", attachment.GetProperty("file_name").GetString());
        Assert.Equal("text/plain", attachment.GetProperty("content_type").GetString());
        Assert.Equal("web", comment.GetProperty("via").GetProperty("channel").GetString());
    }

    [Fact]
    public async Task Comments_Passes_Sideloads_Through_And_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"comments":[{"id":1,"author_id":5,"plain_body":"hi","body":"<p>hi</p>","public":false}],
             "count":1,"users":[{"id":5,"name":"Agent Smith","email":"smith@example.com","role":"agent"}]}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, include: ["users"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/tickets/99/comments", harness.Request.Path);
        // perPage defaults to 10 — explicit on the wire, not left to Zendesk's server default.
        Assert.Equal("?include=users&per_page=10", harness.Request.Query);
        // The users sideload survives under its native name (full view, since comments assemble in full mode).
        var author = Assert.Single(result.GetProperty("users").EnumerateArray());
        Assert.Equal(5, author.GetProperty("id").GetInt64());
        Assert.Equal("Agent Smith", author.GetProperty("name").GetString());
        Assert.Equal("agent", author.GetProperty("role").GetString());
        Assert.False(Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("public").GetBoolean());
    }

    [Fact]
    public async Task Comments_BodyFormat_Rich_Drops_The_Plain_Body()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"comments":[{"id":1,"body":"<p>hi</p>","plain_body":"hi","public":true}],"count":1}""");
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, bodyFormat: "rich",
            cancellationToken: TestContext.Current.CancellationToken);

        var comment = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("<p>hi</p>", comment.GetProperty("body").GetString());
        Assert.False(comment.TryGetProperty("plain_body", out _));
    }

    [Fact]
    public async Task Comments_BodyFormat_Both_Keeps_Both_Bodies_Case_Insensitively()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"comments":[{"id":1,"body":"<p>hi</p>","plain_body":"hi","public":true}],"count":1}""");
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, bodyFormat: "Both",
            cancellationToken: TestContext.Current.CancellationToken);

        var comment = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("<p>hi</p>", comment.GetProperty("body").GetString());
        Assert.Equal("hi", comment.GetProperty("plain_body").GetString());
    }

    [Fact]
    public async Task Comments_Caps_Bodies_With_The_Narrow_Recall_Marker()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"comments":[{"id":1,"plain_body":"0123456789","body":"<p>0123456789</p>","public":true},
             {"id":2,"plain_body":"abcdefghij","body":"<p>abcdefghij</p>","public":true}],"count":30}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, 2, 2, "plain", null, TestContext.Current.CancellationToken, 4);

        // The marker names the exact single-comment re-call: the ABSOLUTE index is computed server-side from
        // the request's page/perPage ((2-1)*2 + position), so the agent never has to do offset arithmetic.
        var items = result.GetProperty("items");
        Assert.Equal(
            "0123…[truncated 6 chars — re-call with maxBodyChars:0 (0 = no limit), perPage:1, page:3 for this comment]",
            items[0].GetProperty("plain_body").GetString());
        Assert.Equal(
            "abcd…[truncated 6 chars — re-call with maxBodyChars:0 (0 = no limit), perPage:1, page:4 for this comment]",
            items[1].GetProperty("plain_body").GetString());
    }

    [Fact]
    public async Task Comments_MaxBodyChars_Zero_Disables_The_Cap()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"comments":[{"id":1,"plain_body":"0123456789","body":"<p>x</p>","public":true}],"count":1}""");
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, cancellationToken: TestContext.Current.CancellationToken,
            maxBodyChars: 0);

        Assert.Equal("0123456789",
            Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("plain_body").GetString());
    }

    [Fact]
    public async Task Comments_Order_Newest_Sends_Sort_Order_Desc_And_The_Marker_Names_The_Order()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"comments":[{"id":9,"plain_body":"0123456789","body":"<p>x</p>","public":true}],"count":20}""");
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, perPage: 10, cancellationToken: TestContext.Current.CancellationToken,
            maxBodyChars: 4, order: "newest");

        // 'newest' rides the OAS-modeled offset sort_order=desc ('oldest'/asc is the API default and adds nothing).
        Assert.Equal("/api/v2/tickets/99/comments", harness.Request.Path);
        Assert.Equal("?per_page=10&sort_order=desc", harness.Request.Query);
        // The absolute index is only valid under the ordering it was computed in, so the marker names it.
        Assert.Equal(
            "0123…[truncated 6 chars — re-call with maxBodyChars:0 (0 = no limit), order:'newest', perPage:1, " +
            "page:1 for this comment]",
            Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("plain_body").GetString());
    }

    [Fact]
    public async Task Comments_Omits_Empty_Attachments_Arrays()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"comments":[{"id":1,"plain_body":"hi","body":"<p>hi</p>","public":true,"attachments":[]}],"count":1}""");
        var tools = CreateTools(harness);

        var result = await tools.Comments(99, cancellationToken: TestContext.Current.CancellationToken);

        // The empty attachments array is pruned to null, then dropped from the full view entirely.
        Assert.False(Assert.Single(result.GetProperty("items").EnumerateArray()).TryGetProperty("attachments", out _));
    }

    [Fact]
    public async Task Comments_Rejects_An_Unknown_Order_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Comments(99, cancellationToken: TestContext.Current.CancellationToken, order: "desc"));

        Assert.Contains("'oldest'", exception.Message);
        Assert.Contains("'newest'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Comments_Rejects_An_Unknown_BodyFormat_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Comments(99, bodyFormat: "markdown", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("'plain'", exception.Message);
        Assert.Contains("'rich'", exception.Message);
        Assert.Contains("'both'", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Comments_Rejects_A_Negative_MaxBodyChars_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Comments(99, cancellationToken: TestContext.Current.CancellationToken, maxBodyChars: -1));

        Assert.Contains("maxBodyChars", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Audits_Requests_Paging_Via_The_PerPage_Escape_Hatch_And_Returns_Summary_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"audits":[{"id":1001,"ticket_id":99,"created_at":"2026-01-01T00:00:00Z",
             "events":[{"id":1,"type":"Change","field_name":"status","value":"open"}]}],
             "count":2,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Audits(99, null, 25, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/audits", request.Path);
        // per_page is not in the generated builder; the escape hatch must still send it.
        Assert.Equal("?per_page=25", request.Query);
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
        var audit = result.GetProperty("items")[0];
        Assert.Equal(1001, audit.GetProperty("id").GetInt64());
        Assert.Equal("status", audit.GetProperty("events")[0].GetProperty("field_name").GetString());
        // Summary rows are allowlisted — fields outside the audit shape do not appear.
        Assert.False(audit.TryGetProperty("ticket_id", out _));
    }

    [Fact]
    public async Task Audits_Passes_Sideloads_And_Page_Through()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"audits":[],"count":0,"users":[{"id":5,"name":"Agent"}]}""");
        var tools = CreateTools(harness);

        var result = await tools.Audits(99, 2, 25, ["users", "groups", "organizations"],
            TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/tickets/99/audits", harness.Request.Path);
        Assert.Equal("?per_page=25&include=users%2Cgroups%2Corganizations&page=2", harness.Request.Query);
        // Sideloaded arrays survive under their native names, summary-projected.
        Assert.Equal(5, result.GetProperty("users")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Audits_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"audits":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Audits(99, cancellationToken: TestContext.Current.CancellationToken);

        // Audits are token-heavy, so the default page is 10 — explicit on the wire.
        Assert.Equal("?per_page=10", harness.Request.Query);
    }

    [Fact]
    public async Task Audits_Summarizes_Comment_Events_And_Drops_Forensic_Metadata()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"audits":[{"id":1002,"created_at":"2026-01-02T00:00:00Z","author_id":5,
             "via":{"channel":"rule","source":{"rel":"trigger","from":{"id":66,"title":"Auto-reply"},
             "to":{"address":"jane@example.com"}}},
             "metadata":{"system":{"ip_address":"10.0.0.1","client":"Mozilla"}},
             "events":[{"id":2,"type":"Comment","public":true,"body":"<p>hello world</p>",
              "html_body":"<p>hello world</p>","plain_body":"hello world"},
              {"id":3,"type":"VoiceComment","public":false,"data":{"recording_url":"https://x/r.mp3"}}]}],
             "count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Audits(99, cancellationToken: TestContext.Current.CancellationToken);

        var audit = result.GetProperty("items")[0];
        // Rule/trigger attribution is kept (via.source.rel/from); forensic metadata is dropped.
        Assert.Equal("trigger", audit.GetProperty("via").GetProperty("source").GetProperty("rel").GetString());
        Assert.Equal("Auto-reply",
            audit.GetProperty("via").GetProperty("source").GetProperty("from").GetProperty("title").GetString());
        Assert.False(audit.TryGetProperty("metadata", out _));
        // Comment events collapse the triple body duplication to a single excerpt.
        var commentEvent = audit.GetProperty("events")[0];
        Assert.Equal("hello world", commentEvent.GetProperty("excerpt").GetString());
        Assert.False(commentEvent.TryGetProperty("body", out _));
        Assert.False(commentEvent.TryGetProperty("plain_body", out _));
        // Voice comments keep only their identity — tickets_comments_list is the sink for the detail.
        var voiceEvent = audit.GetProperty("events")[1];
        Assert.Equal("VoiceComment", voiceEvent.GetProperty("type").GetString());
        Assert.False(voiceEvent.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task Audits_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"audits":[{"id":1003,"url":"https://unit-test.zendesk.com/api/v2/tickets/99/audits/1003.json",
             "author_id":null,"metadata":{"system":{"client":"Mozilla"}},
             "events":[{"id":9,"type":"Comment","body":"full body","plain_body":"full body","public":true}]}],
             "count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Audits(99, null, 25, null, TestContext.Current.CancellationToken, "full");

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var audit = result.GetProperty("items")[0];
        // Full rows keep everything the summary shape strips...
        Assert.True(audit.TryGetProperty("metadata", out _));
        Assert.Equal("full body", audit.GetProperty("events")[0].GetProperty("body").GetString());
        // ...but are still the full VIEW: API self-links and null-valued fields are gone.
        Assert.False(audit.TryGetProperty("url", out _));
        Assert.False(audit.TryGetProperty("author_id", out _));
    }

    [Fact]
    public async Task Metrics_Returns_The_Unwrapped_Ticket_Metric_As_Full_View()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"ticket_metric":{"id":33,"ticket_id":99,"url":"https://unit-test.zendesk.com/api/v2/ticket_metrics/33.json",
             "solved_at":null,"reopens":2,"replies":5,"reply_time_in_minutes":{"calendar":60,"business":30}}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Metrics(99, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/metrics", request.Path);
        Assert.Equal(33, result.GetProperty("id").GetInt64());
        Assert.Equal(2, result.GetProperty("reopens").GetInt32());
        Assert.Equal(30, result.GetProperty("reply_time_in_minutes").GetProperty("business").GetInt32());
        // Full view: the API self-link and null-valued milestones are omitted (absent = not happened yet).
        Assert.False(result.TryGetProperty("url", out _));
        Assert.False(result.TryGetProperty("solved_at", out _));
    }

    [Fact]
    public async Task Metrics_Throws_When_Zendesk_Returns_No_Metrics()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("{}");
        var tools = CreateTools(harness);

        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Metrics(99, TestContext.Current.CancellationToken));

        Assert.Contains("no metrics", exception.Message);
        Assert.Contains("'99'", exception.Message);
    }

    [Fact]
    public async Task Incidents_Requests_The_Problem_Tickets_With_Paging()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":7,"problem_id":500}],"count":4,"next_page":null}""");
        var tools = CreateTools(harness);

        var result = await tools.Incidents(500, null, 25, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/500/incidents", request.Path);
        // The generated incidents builder has no paging parameters; the escape hatch must still send them.
        Assert.Equal("?per_page=25", request.Query);
        var incident = result.GetProperty("items")[0];
        Assert.Equal(7, incident.GetProperty("id").GetInt64());
        Assert.Equal(500, incident.GetProperty("problem_id").GetInt64());
        // The blast-radius number: 'count' is the cheap answer (guidance says perPage:1 + count).
        Assert.Equal(4, result.GetProperty("count").GetInt32());
        Assert.False(result.GetProperty("has_more").GetBoolean());
    }

    [Fact]
    public async Task SideConversations_Requests_The_Spec_Absent_Endpoint()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"side_conversations":[{"id":"sc-1","subject":"Vendor escalation","state":"open"}],
             "count":1,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.SideConversations(99, null, null, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/side_conversations", request.Path);
        Assert.Equal(string.Empty, request.Query);
        var conversation = result.GetProperty("items")[0];
        Assert.Equal("sc-1", conversation.GetProperty("id").GetString());
        Assert.Equal("open", conversation.GetProperty("state").GetString());
        Assert.Equal(1, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task SideConversations_Passes_Paging_Through()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"side_conversations":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.SideConversations(99, 2, 10, TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/tickets/99/side_conversations", harness.Request.Path);
        Assert.Equal("?page=2&per_page=10", harness.Request.Query);
    }

    [Fact]
    public async Task SideConversations_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"side_conversations":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.SideConversations(99, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("?per_page=25", harness.Request.Query);
    }

    [Fact]
    public async Task MetricEvents_Requests_The_Incremental_Export()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"ticket_metric_events":[{"id":1,"ticket_id":155,"metric":"reply_time","instance_id":1,
             "type":"breach","time":"2026-01-01T00:00:00Z"}],"count":1,"end_time":1690000100,
             "end_of_stream":true,"next_page":null}
            """);
        var tools = CreateTools(harness);

        var result = await tools.MetricEvents(1690000000, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/incremental/ticket_metric_events", request.Path);
        Assert.Equal("?start_time=1690000000", request.Query);
        // The export now rides the uniform cursor envelope: raw records land in 'items', Zendesk's end_of_stream
        // becomes has_more (inverted) and end_time is routed into the note as the next startTime to resume from.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        var metricEvent = result.GetProperty("items")[0];
        Assert.Equal(155, metricEvent.GetProperty("ticket_id").GetInt64());
        Assert.Equal("breach", metricEvent.GetProperty("type").GetString());
        // end_of_stream:true → has_more:false; end_time surfaces in the note as the resume startTime.
        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.Contains("startTime:1690000100", result.GetProperty("note").GetString());
        // end_time / end_of_stream are no longer top-level properties (folded into has_more + note).
        Assert.False(result.TryGetProperty("end_time", out _));
        Assert.False(result.TryGetProperty("end_of_stream", out _));
    }

    [Fact]
    public async Task List_Requests_Cursor_Pagination_And_Sideloads()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":5,"subject":"Help"}],"users":[{"id":3,"name":"Agent"}],
             "meta":{"has_more":true,"after_cursor":"cur2"}}
            """);
        var tools = CreateTools(harness);

        var result = await tools.List(50, "cursor-1", ["users"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets", request.Path);
        Assert.Equal("?page%5Bafter%5D=cursor-1&page%5Bsize%5D=50&include=users", request.Query);
        // The lean envelope: metadata first, summary items, sideloads under their native names.
        Assert.Equal("summary", result.GetProperty("detail").GetString());
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
        Assert.Equal(5, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(3, result.GetProperty("users")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task List_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/tickets", harness.Request.Path);
        // The default page size is explicit on the wire — never left to Zendesk's server default of 100.
        Assert.Equal("?page%5Bsize%5D=25", harness.Request.Query);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task List_Treats_A_Blank_Cursor_As_Unset(string blankCursor)
    {
        // Regression: an agent passing afterCursor="" must NOT put an empty page[after] on the wire — Zendesk
        // rejects it with 400 "page[after] is not valid" (observed in production). A blank cursor = first page.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":5}],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.List(50, blankCursor, cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.DoesNotContain("page%5Bafter%5D", request.Query);
        Assert.DoesNotContain("page[after]", Uri.UnescapeDataString(request.Query));
        // The real (non-blank) parameters still go through.
        Assert.Contains("page%5Bsize%5D=50", request.Query);
    }

    [Fact]
    public async Task List_Detail_Full_Returns_Unstripped_Rows()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
             {"tickets":[{"id":5,"subject":"Help","url":"https://unit-test.zendesk.com/api/v2/tickets/5.json",
              "custom_fields":[{"id":1,"value":"x"}],"assignee_id":null}],"meta":{"has_more":false}}

            """);
        var tools = CreateTools(harness);

        var result = await tools.List(detail: "full", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("full", result.GetProperty("detail").GetString());
        var ticket = result.GetProperty("items")[0];
        Assert.True(ticket.TryGetProperty("custom_fields", out _)); // the complete record...
        Assert.False(ticket.TryGetProperty("url", out _)); // ...minus API self-links
        Assert.False(ticket.TryGetProperty("assignee_id", out _)); // ...and null-valued fields
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
    public async Task List_Applies_The_Per_Tool_Response_Budget_And_Suppresses_The_Cursor_On_Truncation()
    {
        var options = new McpOptions();
        options.Tools.MaxResponseCharsByTool["tickets_list"] = 1000;
        var harness = new ZendeskToolHarness();
        var subject = new string('s', 200);
        var rows = string.Join(',', Enumerable.Range(1, 5).Select(i => $$"""{"id":{{i}},"subject":"{{subject}}"}"""));
        harness.EnqueueJson($$$"""{"tickets":[{{{rows}}}],"meta":{"has_more":true,"after_cursor":"cur2"}}""");
        var tools = CreateTools(harness, options: options);

        var result = await tools.List(cancellationToken: TestContext.Current.CancellationToken);

        // Over-budget list responses drop tail items and explain the recovery — and NEVER carry a continuation
        // token (resuming from it would silently skip the dropped items), forcing has_more instead.
        Assert.InRange(result.GetProperty("items").GetArrayLength(), 1, 4);
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.False(result.TryGetProperty("after_cursor", out _));
        Assert.Contains("re-call with pageSize:", result.GetProperty("note").GetString()); // cursor tool → pageSize
    }

    [Fact]
    public async Task ReadMany_Requests_Show_Many_With_Ids_And_Sideloads()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"tickets":[{"id":11,"comment_count":4},{"id":22,"comment_count":1}],"count":2,"next_page":null}""");
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([11, 22], ["comment_count"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/show_many", request.Path);
        Assert.Equal("?ids=11%2C22&include=comment_count", request.Query);
        // Fields materialized by an explicitly requested sideload ride on the summary rows.
        var items = result.GetProperty("items");
        Assert.Equal(11, items[0].GetProperty("id").GetInt64());
        Assert.Equal(4, items[0].GetProperty("comment_count").GetInt32());
        Assert.Equal(2, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ReadMany_Returns_An_Empty_Result_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var result = await tools.ReadMany([], null, TestContext.Current.CancellationToken);

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
            tools.ReadMany(ids, null, TestContext.Current.CancellationToken));

        Assert.Contains("100", exception.Message);
        Assert.Contains("101", exception.Message);
        Assert.Contains("batch", exception.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Count_Returns_The_Unwrapped_Count()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"count":{"value":1234,"refreshed_at":"2026-01-01T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.Count(TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/count", request.Path);
        Assert.Equal(1234, result.GetProperty("value").GetInt64());
        Assert.Equal("2026-01-01T00:00:00Z", result.GetProperty("refreshed_at").GetString());
    }

    [Fact]
    public async Task ReadByExternalId_Requests_The_External_Id()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":5,"external_id":"crm-42"}],"count":1}""");
        var tools = CreateTools(harness);

        var result = await tools.ReadByExternalId("crm-42", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets", request.Path);
        Assert.Equal("?external_id=crm-42", request.Query);
        // external_id is part of the ticket summary shape — the lookup key stays visible on the rows.
        Assert.Equal("crm-42", result.GetProperty("items")[0].GetProperty("external_id").GetString());
    }

    [Fact]
    public async Task ReadByExternalId_Rejects_A_Blank_External_Id()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.ReadByExternalId("   ", TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Collaborators_Requests_The_Ticket_Collaborators()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"users":[{"id":3,"name":"CC'd User","role":"end-user"}],"count":3}""");
        var tools = CreateTools(harness);

        var result = await tools.Collaborators(99, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/collaborators", request.Path);
        var collaborator = result.GetProperty("items")[0];
        Assert.Equal(3, collaborator.GetProperty("id").GetInt64());
        Assert.Equal("end-user", collaborator.GetProperty("role").GetString());
        Assert.Equal(3, result.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task CommentsCount_Returns_The_Unwrapped_Count()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"count":{"value":12,"refreshed_at":"2026-01-01T00:00:00Z"}}""");
        var tools = CreateTools(harness);

        var result = await tools.CommentsCount(99, TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/tickets/99/comments/count", request.Path);
        Assert.Equal(12, result.GetProperty("value").GetInt64());
    }

    [Fact]
    public async Task Incremental_Requests_With_StartTime_And_Sideloads()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"tickets":[{"id":5,"generated_timestamp":1690000001}],"users":[{"id":3,"name":"Agent"}],
             "after_cursor":"next","end_of_stream":false}
            """);
        var tools = CreateTools(harness);

        var result = await tools.Incremental(1690000000, null, ["users"],
            cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/incremental/tickets/cursor", request.Path);
        // per_page is a recorded spec anomaly (live API supports it, the OAS omits it) — explicit default 100.
        Assert.Equal("?include=users&per_page=100&start_time=1690000000", request.Query);
        // The export's top-level after_cursor/end_of_stream are adapted to the uniform envelope.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("next", result.GetProperty("after_cursor").GetString());
        Assert.False(result.TryGetProperty("end_of_stream", out _));
        Assert.Equal(5, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        Assert.Equal(3, result.GetProperty("users")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Incremental_Requests_With_Cursor()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[],"end_of_stream":true}""");
        var tools = CreateTools(harness);

        var result = await tools.Incremental(null, "cursor-2",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/api/v2/incremental/tickets/cursor", harness.Request.Path);
        Assert.Equal("?per_page=100&cursor=cursor-2", harness.Request.Query);
        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.False(result.TryGetProperty("after_cursor", out _));
        Assert.False(result.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task Incremental_Carries_The_Resume_Cursor_In_The_Note_At_End_Of_Stream()
    {
        // At end_of_stream the envelope reports has_more:false (nothing to page NOW), but Zendesk still issues
        // a cursor there — the token that resumes the export later — so it must stay reachable via the note.
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"tickets":[{"id":5}],"after_cursor":"resume-here","end_of_stream":true}""");
        var tools = CreateTools(harness);

        var result = await tools.Incremental(null, "cursor-2",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.GetProperty("has_more").GetBoolean());
        Assert.False(result.TryGetProperty("after_cursor", out _));
        Assert.Contains("resume-here", result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Incremental_Rejects_Passing_Both_Or_Neither_Of_StartTime_And_Cursor()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        var neither = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Incremental(cancellationToken: TestContext.Current.CancellationToken));
        var both = await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Incremental(1690000000, "cursor-2",
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("exactly one of startTime", neither.Message);
        Assert.Contains("exactly one of startTime", both.Message);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task Read_Surfaces_ZendeskApiException_As_McpException_With_Status_And_Body()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueStatus(HttpStatusCode.NotFound, "{\"error\":\"RecordNotFound\"}");
        var tools = CreateTools(harness, true);

        // The MCP SDK discards non-McpException detail; the tool must re-throw an McpException so the agent
        // sees the real status and Zendesk error body (and can distinguish 404 from 403/422) to self-correct.
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            tools.Read(99, TestContext.Current.CancellationToken));

        Assert.Contains("404", exception.Message);
        Assert.Contains("RecordNotFound", exception.Message);
    }

    [Fact]
    public async Task Search_Scopes_To_Tickets_And_Sends_Every_Parameter_On_A_Well_Formed_Url()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """{"results":[{"result_type":"ticket","id":99,"subject":"Printer"}],"count":1,"next_page":null}""");
        var tools = CreateTools(harness);

        var result = await tools.Search("status:open", "created_at", "desc", 2, 50,
            ["users", "organizations"], TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/search", request.Path);
        // Regression guard: the generated search template carries a LITERAL '?query=' — a naive template
        // append would emit a second '?' that corrupts the query server-side and silently drops paging.
        Assert.Equal(1, request.Uri.AbsoluteUri.Count(character => character == '?'));
        var query = Uri.UnescapeDataString(request.Query);
        Assert.Contains("query=type:ticket status:open", query);
        Assert.Contains("sort_by=created_at", query);
        Assert.Contains("sort_order=desc", query);
        Assert.Contains("page=2", query);
        Assert.Contains("per_page=50", query);
        // The Search API sideloads with the nested tickets(...) syntax, unlike flat list sideloads.
        Assert.Contains("include=tickets(users,organizations)", query);
        // The lean envelope keeps the search metadata: count plus the row's result_type discriminator.
        Assert.Equal(1, result.GetProperty("count").GetInt32());
        var item = result.GetProperty("items")[0];
        Assert.Equal(99, item.GetProperty("id").GetInt64());
        Assert.Equal("ticket", item.GetProperty("result_type").GetString());
    }

    [Fact]
    public async Task Search_Returns_Summary_Rows_By_Default_And_Full_Rows_On_Detail_Full()
    {
        const string fixture =
            """
            {"results":[{"result_type":"ticket","id":99,"subject":"Printer","status":"open",
              "url":"https://unit-test.zendesk.com/api/v2/tickets/99.json","custom_fields":[{"id":1,"value":"x"}],
              "fields":[{"id":1,"value":"x"}],"description":"a long description","raw_subject":"Printer raw"}],"count":1}
            """;

        // Default: allowlisted summary rows — the token-heavy members are gone, the triage fields remain.
        var lean = new ZendeskToolHarness();
        lean.EnqueueJson(fixture);
        var leanResult = await CreateTools(lean).Search("status:open",
            cancellationToken: TestContext.Current.CancellationToken);
        var leanTicket = leanResult.GetProperty("items")[0];
        Assert.Equal(99, leanTicket.GetProperty("id").GetInt64());
        Assert.Equal("open", leanTicket.GetProperty("status").GetString());
        Assert.Equal("a long description", leanTicket.GetProperty("description").GetString()); // 150-char excerpt
        Assert.False(leanTicket.TryGetProperty("custom_fields", out _));
        Assert.False(leanTicket.TryGetProperty("fields", out _));
        Assert.False(leanTicket.TryGetProperty("raw_subject", out _));
        Assert.False(leanTicket.TryGetProperty("url", out _));

        // detail:'full': the complete record (minus API self-links) is returned.
        var full = new ZendeskToolHarness();
        full.EnqueueJson(fixture);
        var fullResult = await CreateTools(full).Search("status:open", detail: "full",
            cancellationToken: TestContext.Current.CancellationToken);
        var fullTicket = fullResult.GetProperty("items")[0];
        Assert.True(fullTicket.TryGetProperty("custom_fields", out _));
        Assert.Equal("Printer raw", fullTicket.GetProperty("raw_subject").GetString());
        Assert.False(fullTicket.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Search_Respects_A_Caller_Type_Selector_And_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"results":[],"count":0}""");
        var tools = CreateTools(harness);

        await tools.Search("type:ticket tags:vip", cancellationToken: TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(1, request.Uri.AbsoluteUri.Count(character => character == '?'));
        var query = Uri.UnescapeDataString(request.Query);
        // Caller already scoped the query — no double type: selector.
        Assert.Contains("query=type:ticket tags:vip", query);
        Assert.DoesNotContain("type:ticket type:ticket", query);
        // perPage defaults to 25 and must survive the literal-query template.
        Assert.Contains("per_page=25", query);
    }

    [Fact]
    public async Task Search_Rejects_A_Blank_Query_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.Search("   ", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task SearchExport_Sends_The_Ticket_Filter_And_Cursor_Parameters()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson(
            """
            {"results":[{"result_type":"ticket","id":7}],"meta":{"has_more":true,"after_cursor":"cur2"},"count":1}
            """);
        var tools = CreateTools(harness);

        var result = await tools.SearchExport("tags:vip", 100, "cur1", TestContext.Current.CancellationToken);

        var request = harness.Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v2/search/export", request.Path);
        var query = Uri.UnescapeDataString(request.Query);
        Assert.Contains("filter[type]=ticket", query);
        Assert.Contains("query=tags:vip", query);
        Assert.Contains("page[size]=100", query);
        Assert.Contains("page[after]=cur1", query);
        // Cursor continuation metadata rides at the top of the lean envelope.
        Assert.True(result.GetProperty("has_more").GetBoolean());
        Assert.Equal("cur2", result.GetProperty("after_cursor").GetString());
        Assert.Equal(7, result.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task SearchExport_Applies_The_Default_Page_Size()
    {
        var harness = new ZendeskToolHarness();
        harness.EnqueueJson("""{"results":[],"meta":{"has_more":false}}""");
        var tools = CreateTools(harness);

        await tools.SearchExport("tags:vip", cancellationToken: TestContext.Current.CancellationToken);

        var query = Uri.UnescapeDataString(harness.Request.Query);
        Assert.Contains("page[size]=100", query);
        Assert.Contains("filter[type]=ticket", query);
    }

    [Fact]
    public async Task SearchExport_Rejects_A_Blank_Query_Without_Calling_Zendesk()
    {
        var harness = new ZendeskToolHarness();
        var tools = CreateTools(harness);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.SearchExport(" ", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(harness.Requests);
    }
}