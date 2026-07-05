using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     Per-entity allowlist shapes: a fat (production-shaped) fixture goes in, exactly the design's lean summary
///     row comes out. Heavy fields (custom fields, bodies, actions, conditions, raw email content...) must be
///     GONE, not merely smaller.
/// </summary>
public class ZendeskLeanSummaryShapeTests
{
    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static JsonObject Summarize(string arrayName, string json)
    {
        var row = ZendeskLean.SummarizeEntity(arrayName, Parse(json));
        Assert.NotNull(row);
        return row;
    }

    [Fact]
    public void Ticket_Row_Keeps_The_Triage_Fields_And_Truncates_The_Description()
    {
        var row = Summarize("tickets",
            $$"""
              {"id":35436,"url":"https://acme.zendesk.com/api/v2/tickets/35436.json","subject":"Help!",
               "raw_subject":"Help! raw","description":"{{new string('d', 200)}}","status":"open",
               "priority":"high","type":"incident","due_at":"2026-08-01T00:00:00Z",
               "created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-02T00:00:00Z","requester_id":20978392,
               "assignee_id":235323,"group_id":98738,"organization_id":509974,"custom_status_id":1001,
               "ticket_form_id":47,"problem_id":9873764,"external_id":"ahg35h3jh","tags":["enterprise","other"],
               "via":{"channel":"web","source":{"from":{},"to":{},"rel":null} },
               "custom_fields":[{"id":27642,"value":"745"}],"fields":[{"id":27642,"value":"745"}],
               "satisfaction_rating":{"comment":"great","id":1234,"score":"good"},
               "sharing_agreement_ids":[84432],"followup_ids":[],"forum_topic_id":null}
              """);

        Assert.Equal(
            new[]
            {
                "id", "subject", "description", "status", "priority", "type", "due_at", "created_at", "updated_at",
                "requester_id", "assignee_id", "group_id", "organization_id", "custom_status_id", "ticket_form_id",
                "problem_id", "external_id", "tags", "via"
            },
            row.Select(property => property.Key));
        Assert.Equal(35436, row["id"]!.GetValue<long>());
        // The description (== the first comment) is an excerpt: 150 chars + ellipsis.
        Assert.Equal(new string('d', 150) + "…", row["description"]!.GetValue<string>());
        Assert.Equal("2026-08-01T00:00:00Z", row["due_at"]!.GetValue<string>());
        // via collapses to just the channel.
        Assert.Equal("""{"channel":"web"}""", row["via"]!.ToJsonString());
    }

    [Fact]
    public void Ticket_Row_Omits_Absent_And_Empty_Fields()
    {
        var row = Summarize("tickets", """{"id":1,"subject":"s","description":"","status":"new"}""");

        Assert.Equal(new[] { "id", "subject", "status" }, row.Select(property => property.Key));
    }

    [Fact]
    public void Ticket_Row_Appends_Explicitly_Requested_Sideload_Fields()
    {
        // comment_count is materialized only when its sideload was requested — the tool appends it per call.
        var row = ZendeskLean.SummarizeEntity("tickets",
            Parse("""{"id":1,"subject":"s","comment_count":7,"custom_fields":[{"id":2}]}"""), ["comment_count"]);

        Assert.NotNull(row);
        Assert.Equal(7, row["comment_count"]!.GetValue<int>());
        Assert.False(row.ContainsKey("custom_fields"));
    }

    [Fact]
    public void User_Row_Drops_Photo_And_Custom_Fields()
    {
        var row = Summarize("users",
            """
            {"id":9873843,"url":"https://acme.zendesk.com/api/v2/users/9873843.json","name":"Sam",
             "email":"sam@example.org","role":"agent","active":true,"suspended":false,"organization_id":57542,
             "phone":"+15551234567","last_login_at":"2026-07-01T00:00:00Z","external_id":"ext-55",
             "photo":{"id":928374,"content_url":"https://...","thumbnails":[{"id":1}]},
             "user_fields":{"membership":"gold"},"notes":"long agent notes","details":"more","signature":"sig",
             "tags":["vip"],"time_zone":"Copenhagen"}
            """);

        Assert.Equal(
            new[]
            {
                "id", "name", "email", "role", "active", "suspended", "organization_id", "phone", "last_login_at",
                "external_id"
            },
            row.Select(property => property.Key));
        Assert.Equal("sam@example.org", row["email"]!.GetValue<string>());
    }

    [Fact]
    public void Organization_Row_Drops_Custom_Fields_Notes_And_Details()
    {
        var row = Summarize("organizations",
            """
            {"id":509974,"url":"https://acme.zendesk.com/api/v2/organizations/509974.json","name":"Acme",
             "domain_names":["acme.example"],"external_id":"org-9","shared_tickets":true,"shared_comments":false,
             "tags":["enterprise"],"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-06-01T00:00:00Z",
             "organization_fields":{"tier":null,"region":"emea"},"notes":"long notes","details":"long details"}
            """);

        Assert.Equal(
            new[]
            {
                "id", "name", "domain_names", "external_id", "shared_tickets", "shared_comments", "tags",
                "created_at", "updated_at"
            },
            row.Select(property => property.Key));
    }

    [Fact]
    public void Article_Row_Keeps_The_Permalink_And_The_Search_Snippet_But_Never_The_Body()
    {
        var row = Summarize("articles",
            """
            {"id":37486578,"url":"https://acme.zendesk.com/api/v2/help_center/articles/37486578.json",
             "html_url":"https://acme.zendesk.com/hc/en-us/articles/37486578","title":"How to reset",
             "section_id":98838,"locale":"en-us","draft":false,"promoted":true,"label_names":["reset"],
             "updated_at":"2026-05-05T00:00:00Z","snippet":"How to <em>reset</em> your password",
             "body":"<p>enormous html body</p>","vote_sum":12,"vote_count":30,"outdated":false}
            """);

        Assert.Equal(
            new[]
            {
                "id", "title", "html_url", "section_id", "locale", "draft", "promoted", "label_names",
                "updated_at", "snippet"
            },
            row.Select(property => property.Key));
        // The snippet keeps its <em> relevance markers — a cheap, documented relevance signal.
        Assert.Equal("How to <em>reset</em> your password", row["snippet"]!.GetValue<string>());
    }

    [Fact]
    public void Group_Row_Is_The_Small_Allowlist()
    {
        var row = Summarize("groups",
            """
            {"id":98738,"url":"https://acme.zendesk.com/api/v2/groups/98738.json","name":"Tier 1",
             "description":"Front-line support","default":false,"deleted":false,"is_public":true,
             "created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}
            """);

        Assert.Equal(new[] { "id", "name", "description", "default", "deleted", "is_public" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void Macro_Row_Strips_The_Actions_But_Keeps_Usage_Counters_When_Present()
    {
        var row = Summarize("macros",
            """
            {"id":25,"url":"https://acme.zendesk.com/api/v2/macros/25.json","title":"Close and thank",
             "active":true,"description":"Thanks the requester","usage_7d":18,"usage_30d":90,
             "actions":[{"field":"status","value":"solved"},{"field":"comment_value","value":"huge canned reply"}],
             "restriction":{"type":"Group","ids":[1]},"raw_title":"Close and thank"}
            """);

        Assert.Equal(new[] { "id", "title", "active", "description", "usage_7d", "usage_30d" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void View_Row_Strips_Conditions_Execution_And_Restriction()
    {
        var row = Summarize("views",
            """
            {"id":25,"url":"https://acme.zendesk.com/api/v2/views/25.json","title":"Unassigned","active":true,
             "default":false,"position":3,
             "conditions":{"all":[{"field":"status","operator":"is","value":"open"}],"any":[]},
             "execution":{"columns":[{"id":"status","title":"Status"}],"group_by":"assignee"},
             "restriction":{"type":"Group","ids":[1]}}
            """);

        Assert.Equal(new[] { "id", "title", "active", "default", "position" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void TicketField_Row_Replaces_Options_With_A_Computed_Count()
    {
        var row = Summarize("ticket_fields",
            """
            {"id":89,"url":"https://acme.zendesk.com/api/v2/ticket_fields/89.json","type":"tagger",
             "title":"Severity","active":true,"required":false,"description":"Pick one",
             "custom_field_options":[{"id":1,"name":"Low","value":"sev_low"},{"id":2,"name":"High","value":"sev_high"},
              {"id":3,"name":"Critical","value":"sev_crit"}],
             "raw_title":"Severity","raw_description":"Pick one","agent_description":"internal"}
            """);

        Assert.Equal(new[] { "id", "type", "title", "active", "required", "options_count" },
            row.Select(property => property.Key));
        Assert.Equal(3, row["options_count"]!.GetValue<int>());
    }

    [Fact]
    public void TicketField_Row_Counts_System_Field_Options_When_There_Are_No_Custom_Ones()
    {
        var row = Summarize("ticket_fields",
            """
            {"id":1,"type":"status","title":"Status","active":true,"required":true,
             "system_field_options":[{"name":"Open","value":"open"},{"name":"Solved","value":"solved"}]}
            """);

        Assert.Equal(2, row["options_count"]!.GetValue<int>());
    }

    [Fact]
    public void TicketForm_Row_Strips_The_Condition_Trees()
    {
        var row = Summarize("ticket_forms",
            """
            {"id":47,"url":"https://acme.zendesk.com/api/v2/ticket_forms/47.json","name":"Default form",
             "active":true,"default":true,"position":1,"ticket_field_ids":[2,4,8],
             "agent_conditions":[{"parent_field_id":2,"value":"x","child_fields":[{"id":4}]}],
             "end_user_conditions":[],"raw_name":"Default form","display_name":"Default"}
            """);

        Assert.Equal(new[] { "id", "name", "active", "default", "position", "ticket_field_ids" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void Brand_Row_Strips_The_Logo_And_Signature()
    {
        var row = Summarize("brands",
            """
            {"id":47,"url":"https://acme.zendesk.com/api/v2/brands/47.json","name":"Acme Support",
             "subdomain":"acme","active":true,"default":true,"has_help_center":true,
             "logo":{"id":928374,"content_url":"https://...","thumbnails":[{"id":1},{"id":2}]},
             "signature_template":"{{agent.signature}}","host_mapping":"support.acme.example"}
            """);

        Assert.Equal(new[] { "id", "name", "subdomain", "active", "default", "has_help_center" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void CustomStatus_Row_Drops_The_Raw_Label_Duplicates()
    {
        var row = Summarize("custom_statuses",
            """
            {"id":1001,"url":"https://acme.zendesk.com/api/v2/custom_statuses/1001.json",
             "status_category":"hold","agent_label":"Awaiting vendor","active":true,
             "raw_agent_label":"Awaiting vendor","end_user_label":"On hold","raw_end_user_label":"On hold",
             "description":"desc","raw_description":"desc"}
            """);

        Assert.Equal(new[] { "id", "status_category", "agent_label", "active" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void SuspendedTicket_Row_Strips_The_Raw_Email_Content_And_Trims_The_Author()
    {
        var row = Summarize("suspended_tickets",
            """
            {"id":3436,"url":"https://acme.zendesk.com/api/v2/suspended_tickets/3436.json",
             "subject":"Help, my printer is on fire","cause":"Detected as spam",
             "author":{"id":null,"name":"Sender","email":"sender@example.org"},
             "brand_id":47,"ticket_id":9873,"created_at":"2026-07-01T00:00:00Z",
             "content":"Full RAW inbound email with headers and quoted thread...","via":{"channel":"email"}}
            """);

        Assert.Equal(new[] { "id", "subject", "cause", "author", "brand_id", "ticket_id", "created_at" },
            row.Select(property => property.Key));
        Assert.Equal("""{"name":"Sender","email":"sender@example.org"}""", row["author"]!.ToJsonString());
    }

    [Fact]
    public void Identity_Row_Is_The_Small_Allowlist()
    {
        var row = Summarize("identities",
            """
            {"id":35436,"url":"https://acme.zendesk.com/api/v2/users/135/identities/35436.json","user_id":135,
             "type":"email","value":"sam@example.org","primary":true,"verified":true,
             "created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}
            """);

        Assert.Equal(new[] { "id", "user_id", "type", "value", "primary", "verified" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void Attachment_Row_Keeps_The_Metadata_And_Drops_Urls_And_Thumbnails()
    {
        var row = Summarize("attachments",
            """
            {"id":928374,"url":"https://acme.zendesk.com/api/v2/attachments/928374.json",
             "file_name":"error.log","content_type":"text/plain","size":2048,"inline":false,
             "malware_scan_result":"malware_not_found","content_url":"https://acme.zendesk.com/attachments/...",
             "mapped_content_url":"https://...","thumbnails":[{"id":1,"content_url":"https://..."}]}
            """);

        Assert.Equal(new[] { "id", "file_name", "content_type", "size", "inline", "malware_scan_result" },
            row.Select(property => property.Key));
    }

    [Fact]
    public void SideConversation_Row_Trims_Participants_And_Truncates_The_Preview()
    {
        var row = Summarize("side_conversations",
            $$"""
              {"id":"c6d0c5f8","url":"https://acme.zendesk.com/api/v2/tickets/35436/side_conversations/c6d0c5f8",
               "subject":"Vendor escalation","state":"open","created_at":"2026-07-01T00:00:00Z",
               "message_added_at":"2026-07-02T00:00:00Z",
               "participants":[{"user_id":135,"name":"Sam","email":"sam@example.org","slack_workspace_id":null}],
               "preview_text":"{{new string('p', 250)}}","external_ids":{"targetTicketId":"9"} }
              """);

        Assert.Equal(
            new[] { "id", "subject", "state", "created_at", "message_added_at", "participants", "preview_text" },
            row.Select(property => property.Key));
        Assert.Equal("""[{"user_id":135,"email":"sam@example.org"}]""", row["participants"]!.ToJsonString());
        Assert.Equal(new string('p', 200) + "…", row["preview_text"]!.GetValue<string>());
    }

    [Fact]
    public void JobStatus_Row_Collapses_Results_To_A_Summary_With_At_Most_Five_Failures()
    {
        // 3 successes + 7 failures: counts must be exact, embedded failures capped at 5.
        var results = string.Join(',',
            Enumerable.Range(0, 3).Select(i => $$"""{"id":{{i}},"index":{{i}},"success":true,"status":"Updated"}""")
                .Concat(Enumerable.Range(3, 7).Select(i =>
                    $$"""{"id":{{i}},"index":{{i}},"error":"TooManyValues","details":"boom"}""")));
        var row = Summarize("job_statuses",
            $$"""
              {"id":"8b726e606741012ffc2d782bcb7848fe","url":"https://acme.zendesk.com/api/v2/job_statuses/8b.json",
               "status":"completed","progress":10,"total":10,"message":"Completed at 2026-07-04",
               "results":[{{results}}]}
              """);

        Assert.Equal(new[] { "id", "status", "progress", "total", "message", "results_summary" },
            row.Select(property => property.Key));
        var summary = (JsonObject)row["results_summary"]!;
        Assert.Equal(3, summary["succeeded"]!.GetValue<int>());
        Assert.Equal(7, summary["failed"]!.GetValue<int>());
        var failures = (JsonArray)summary["failures"]!;
        Assert.Equal(5, failures.Count);
        Assert.Equal("""{"index":3,"id":3,"error":"TooManyValues"}""", failures[0]!.ToJsonString());
    }

    [Fact]
    public void JobStatus_Row_Omits_The_Summary_While_The_Job_Is_Still_Queued()
    {
        var row = Summarize("job_statuses",
            """{"id":"8b72","status":"queued","progress":null,"total":100,"message":null}""");

        Assert.Equal(new[] { "id", "status", "total" }, row.Select(property => property.Key));
    }

    [Fact]
    public void Audit_Row_Keeps_Rule_Attribution_And_Drops_The_Forensic_Metadata()
    {
        var row = Summarize("audits",
            """
            {"id":9873843,"ticket_id":35436,"created_at":"2026-07-01T00:00:00Z","author_id":-1,
             "metadata":{"system":{"ip_address":"1.2.3.4","latitude":55.6,"longitude":12.5},"custom":{}},
             "via":{"channel":"rule","source":{"rel":"trigger","from":{"id":98,"title":"Notify requester","deleted":false},"to":{}}},
             "events":[{"id":1,"type":"Change","field_name":"status","previous_value":"open","value":"solved",
               "url":"https://..."}]}
            """);

        Assert.Equal(new[] { "id", "created_at", "author_id", "via", "events" },
            row.Select(property => property.Key));
        Assert.Equal("""{"channel":"rule","source":{"rel":"trigger","from":{"id":98,"title":"Notify requester"}}}""",
            row["via"]!.ToJsonString());
        Assert.Equal("""{"id":1,"type":"Change","field_name":"status","previous_value":"open","value":"solved"}""",
            ((JsonArray)row["events"]!)[0]!.ToJsonString());
    }

    [Fact]
    public void Audit_Comment_Event_Collapses_The_Triple_Body_To_A_PlainBody_Excerpt()
    {
        var row = Summarize("audits",
            $$"""
              {"id":1,"created_at":"2026-07-01T00:00:00Z","author_id":135,
               "events":[{"id":2,"type":"Comment","public":true,
                 "body":"markdown body","html_body":"<p>html body</p>","plain_body":"{{new string('x', 250)}}",
                 "attachments":[{"id":1,"content_url":"https://..."}],"audit_id":1}]}
              """);

        var commentEvent = (JsonObject)((JsonArray)row["events"]!)[0]!;
        Assert.Equal(new[] { "id", "type", "public", "excerpt" }, commentEvent.Select(property => property.Key));
        Assert.Equal(new string('x', 200) + "…", commentEvent["excerpt"]!.GetValue<string>());
    }

    [Fact]
    public void Audit_Comment_Event_Excerpt_Falls_Back_To_Body_When_PlainBody_Is_Missing()
    {
        var row = Summarize("audits",
            """
            {"id":1,"created_at":"2026-07-01T00:00:00Z","author_id":135,
             "events":[{"id":2,"type":"Comment","public":false,"body":"only the body survives"}]}
            """);

        var commentEvent = (JsonObject)((JsonArray)row["events"]!)[0]!;
        Assert.Equal("only the body survives", commentEvent["excerpt"]!.GetValue<string>());
    }

    [Fact]
    public void Audit_VoiceComment_Event_Keeps_Only_Its_Identity()
    {
        var row = Summarize("audits",
            """
            {"id":1,"created_at":"2026-07-01T00:00:00Z","author_id":135,
             "events":[{"id":3,"type":"VoiceComment","public":true,
               "data":{"recording_url":"https://...","transcription_text":"very long transcript","call_id":77},
               "body":"Call from +45...","html_body":"<p>Call from...</p>"}]}
            """);

        var voiceEvent = (JsonObject)((JsonArray)row["events"]!)[0]!;
        Assert.Equal(new[] { "id", "type", "public" }, voiceEvent.Select(property => property.Key));
    }

    [Fact]
    public void Section_And_Category_Rows_Share_The_Shape_And_Truncate_The_Description()
    {
        var section = Summarize("sections",
            $$"""
              {"id":98838,"url":"https://acme.zendesk.com/api/v2/help_center/sections/98838.json",
               "html_url":"https://acme.zendesk.com/hc/en-us/sections/98838","name":"Billing",
               "description":"{{new string('s', 250)}}","category_id":112,"parent_section_id":null,"position":2,
               "updated_at":"2026-06-01T00:00:00Z","locale":"en-us","outdated":false}
              """);
        Assert.Equal(new[] { "id", "name", "html_url", "description", "category_id", "position", "updated_at" },
            section.Select(property => property.Key));
        Assert.Equal(new string('s', 200) + "…", section["description"]!.GetValue<string>());

        var category = Summarize("categories",
            """
            {"id":112,"html_url":"https://acme.zendesk.com/hc/en-us/categories/112","name":"FAQ",
             "description":"Common questions","position":1,"updated_at":"2026-06-01T00:00:00Z"}
            """);
        Assert.Equal(new[] { "id", "name", "html_url", "description", "position", "updated_at" },
            category.Select(property => property.Key));
    }

    [Fact]
    public void SummarizeEntity_Returns_Null_For_An_Unregistered_Array_Name()
    {
        Assert.Null(ZendeskLean.SummarizeEntity("metric_sets", Parse("""{"id":1}""")));
    }

    [Fact]
    public void HasSummaryShape_Knows_The_Registered_Arrays_And_The_Search_Results_Dispatch()
    {
        Assert.True(ZendeskLean.HasSummaryShape("tickets"));
        Assert.True(ZendeskLean.HasSummaryShape("results"));
        Assert.True(ZendeskLean.HasSummaryShape("audits"));
        Assert.False(ZendeskLean.HasSummaryShape("metric_sets"));
    }
}