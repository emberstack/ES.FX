using System.Reflection;
using System.Text.Json;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     The poisoned-fixture canary sweep (design A5): the "42 unprojected tools" failure mode must be
///     unrepeatable. Every read-side list/search/sublist tool in the host assembly is invoked — by reflection,
///     so a NEWLY ADDED tool is swept automatically unless it is put on the explicit exclusion list below —
///     against one poisoned Zendesk response carrying every known entity array, whose rows are stuffed with
///     sentinel values in exactly the fields the lean design strips (API self-link urls, bodies, custom fields,
///     macro actions, view conditions, raw email content, photos/logos/signatures, notes/details, and text
///     beyond the excerpt caps). The sweep then asserts, for the DEFAULT (summary) response of each tool:
///     (a) not a single sentinel survives serialization, and (b) the response carries the uniform lean list
///     envelope (metadata-first, rows in <c>items</c>). A tool routed around the <see cref="ZendeskLean" />
///     gateway leaks the sentinel and fails loudly, naming itself.
/// </summary>
public class ZendeskLeanPoisonedCanarySweepTests
{
    /// <summary>The read-side tools the sweep must cover; bump deliberately when the tool surface grows.</summary>
    private const int ExpectedSweptTools = 56;

    /// <summary>The canary marker planted in every field the lean projection is supposed to strip.</summary>
    private const string Sentinel = "__CANARY_LEAK__";

    /// <summary>
    ///     The EXPLICIT exclusion allowlist: read tools that are not lean list envelopes by design. Every entry
    ///     must name an existing read tool (a stale entry fails the sweep) — and everything NOT listed here is
    ///     swept, so a new unrouted list tool cannot slip in unnoticed.
    /// </summary>
    private static readonly IReadOnlySet<string> ExcludedReadTools = new HashSet<string>(StringComparer.Ordinal)
    {
        // ---- Full-view detail sinks (design A1/A2): single-record readers that intentionally return the
        //      complete record (minus nulls/API links). They are the documented escalation path for summary rows.
        "articles_categories_get", "articles_sections_get", "brands_get", "custom_statuses_get", "forms_get",
        "groups_get", "macros_get", "organizations_get", "suspended_tickets_get", "ticket_fields_get",
        "tickets_get", "tickets_metrics_get", "users_get", "users_me_get", "views_get",
        // Single-record status readers: an org-merge status blob and the related-counts record (both tiny),
        // and job_statuses_get, which returns ONE lean-by-status record, not a list envelope.
        "organizations_merges_get", "users_related_get", "job_statuses_get",
        // Macro apply previews: single-record full-view reads (the macro's would-be ticket changes / the
        // ticket-after-changes), not list envelopes — detail sinks like the other *_get readers.
        "macros_changes_get", "macros_ticket_preview_get",
        // Custom object record detail sink (full-view single record).
        "custom_objects_records_get",
        // articles_get: detail sink with its own bodyFormat conversion + maxBodyChars cap (design B6).
        "articles_get",
        // ticket_fields_get_many: deliberately FULL-view rows — it IS the detail sink for decoding a ticket's
        // custom_fields (design B5); only its per-field options are capped.
        "ticket_fields_get_many",

        // ---- Count/scalar tools: they return a number (or a tiny {value, refreshed_at} record) — no rows.
        "groups_count", "groups_users_count", "organizations_count", "organizations_tickets_count",
        "organizations_users_count", "satisfaction_ratings_count", "search_count", "tags_count",
        "tickets_comments_count", "tickets_count", "users_count", "views_count", "views_count_many",
        // Single-record detail sink (like the other *_get readers).
        "satisfaction_ratings_get",

        // ---- Bespoke projections with their own dedicated coverage.
        "tickets_comments_list", // typed comment projection: bodyFormat/maxBodyChars/order (design B3)
        "attachments_get", // byte-capped content download; exempt from the char guard by design (B6)
        "ticket_fields_options_list", // the designated complete-options sink ticket_fields_get points at (B6)
        "tags_autocomplete", // plain string-array payload ({ "tags": ["name", ...] })
        "users_tags_list", // plain string-array payload
        "organizations_tags_list", // plain string-array payload
        "tickets_metric_events_export" // raw incremental-export passthrough; guidance-only by design (B6)
    };

    /// <summary>
    ///     One poisoned Zendesk response served to every swept tool: every known entity array name (primary
    ///     arrays and sideloads alike), each row carrying its legitimate summary fields PLUS sentinels in the
    ///     fields the design strips. Long texts place the sentinel BEYOND the excerpt caps (150 chars for ticket
    ///     descriptions, 200 for excerpts/previews), so correct truncation removes it. Continuation metadata is
    ///     poisoned too: absolute next_page/previous_page/links URLs must never be echoed.
    /// </summary>
    private static readonly string PoisonedResponse =
        $$"""
          {
            "count": 3,
            "next_page": "https://unit-test.zendesk.com/api/v2/things.json?page=2&leak={{Sentinel}}",
            "previous_page": "https://unit-test.zendesk.com/api/v2/things.json?page=1&leak={{Sentinel}}",
            "links": { "next": "https://unit-test.zendesk.com/api/v2/things.json?page[after]=x&leak={{Sentinel}}" },
            "meta": { "has_more": true, "after_cursor": "cursor-continue" },
            "after_cursor": "cursor-continue",
            "end_of_stream": false,
            "tickets": [
              { "id": 101, "url": "https://unit-test.zendesk.com/api/v2/tickets/101.json?leak={{Sentinel}}",
                "subject": "Printer on fire", "raw_subject": "{{Sentinel}} raw subject",
                "description": "{{new string('d', 150)}}{{Sentinel}}",
                "status": "open", "priority": "high", "type": "incident", "due_at": "2026-08-01T00:00:00Z",
                "created_at": "2026-07-01T00:00:00Z", "updated_at": "2026-07-02T00:00:00Z",
                "requester_id": 1, "assignee_id": 2, "group_id": 3, "organization_id": 4,
                "custom_status_id": 5, "ticket_form_id": 6, "problem_id": 7, "external_id": "ext-9",
                "tags": ["vip"],
                "via": { "channel": "web", "source": { "from": { "address": "{{Sentinel}}@example.org" }, "to": {}, "rel": null } },
                "custom_fields": [ { "id": 1, "value": "{{Sentinel}} custom field" } ],
                "fields": [ { "id": 1, "value": "{{Sentinel}} field" } ],
                "satisfaction_rating": { "score": "good", "comment": "{{Sentinel}} csat" },
                "comment_count": 7 }
            ],
            "users": [
              { "id": 201, "url": "https://unit-test.zendesk.com/api/v2/users/201.json?leak={{Sentinel}}",
                "name": "Sam Agent", "email": "sam@example.org", "role": "agent", "active": true,
                "suspended": false, "organization_id": 4, "phone": "+15550000000",
                "last_login_at": "2026-07-01T00:00:00Z", "external_id": "u-ext",
                "photo": { "content_url": "https://cdn.example/{{Sentinel}}.png", "thumbnails": [ { "content_url": "{{Sentinel}}" } ] },
                "notes": "{{Sentinel}} notes", "details": "{{Sentinel}} details",
                "signature": "{{Sentinel}} signature", "user_fields": { "tier": "{{Sentinel}} gold" } }
            ],
            "organizations": [
              { "id": 301, "url": "https://unit-test.zendesk.com/api/v2/organizations/301.json?leak={{Sentinel}}",
                "name": "Acme", "domain_names": ["acme.example"], "external_id": "org-9",
                "shared_tickets": true, "shared_comments": false, "tags": ["enterprise"],
                "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-06-01T00:00:00Z",
                "organization_fields": { "region": "{{Sentinel}} emea" },
                "notes": "{{Sentinel}} notes", "details": "{{Sentinel}} details" }
            ],
            "articles": [
              { "id": 401, "url": "https://unit-test.zendesk.com/api/v2/help_center/articles/401.json?leak={{Sentinel}}",
                "html_url": "https://unit-test.zendesk.com/hc/en-us/articles/401", "title": "How to reset",
                "section_id": 42, "locale": "en-us", "draft": false, "promoted": true,
                "label_names": ["reset"], "updated_at": "2026-05-05T00:00:00Z",
                "snippet": "how to <em>reset</em>",
                "body": "<p>{{Sentinel}} enormous html body</p>",
                "translations": [ { "locale": "da", "body": "{{Sentinel}} translated body" } ] }
            ],
            "groups": [
              { "id": 3, "url": "https://unit-test.zendesk.com/api/v2/groups/3.json?leak={{Sentinel}}",
                "name": "Tier 1", "description": "Front line", "default": false, "deleted": false,
                "is_public": true, "created_at": "2026-01-01T00:00:00Z" }
            ],
            "macros": [
              { "id": 25, "url": "https://unit-test.zendesk.com/api/v2/macros/25.json?leak={{Sentinel}}",
                "title": "Close and thank", "active": true, "description": "Thanks the requester",
                "usage_7d": 18, "usage_30d": 90, "raw_title": "{{Sentinel}} raw title",
                "actions": [ { "field": "comment_value", "value": "{{Sentinel}} huge canned reply" } ],
                "restriction": { "type": "Group", "ids": [1], "note": "{{Sentinel}}" } }
            ],
            "views": [
              { "id": 31, "url": "https://unit-test.zendesk.com/api/v2/views/31.json?leak={{Sentinel}}",
                "title": "Unassigned", "active": true, "default": false, "position": 3,
                "conditions": { "all": [ { "field": "status", "operator": "is", "value": "{{Sentinel}} open" } ], "any": [] },
                "execution": { "columns": [ { "id": "status", "title": "{{Sentinel}} Status" } ] },
                "restriction": { "type": "Group", "ids": [1], "note": "{{Sentinel}}" } }
            ],
            "ticket_fields": [
              { "id": 89, "url": "https://unit-test.zendesk.com/api/v2/ticket_fields/89.json?leak={{Sentinel}}",
                "type": "tagger", "title": "Severity", "active": true, "required": false,
                "description": "{{Sentinel}} pick one", "raw_title": "{{Sentinel}} raw",
                "agent_description": "{{Sentinel}} internal",
                "custom_field_options": [ { "id": 1, "name": "{{Sentinel}} Low", "value": "sev_low" } ] }
            ],
            "ticket_forms": [
              { "id": 6, "url": "https://unit-test.zendesk.com/api/v2/ticket_forms/6.json?leak={{Sentinel}}",
                "name": "Default form", "active": true, "default": true, "position": 1,
                "ticket_field_ids": [2, 4], "raw_name": "{{Sentinel}} raw name",
                "agent_conditions": [ { "parent_field_id": 2, "value": "{{Sentinel}}" } ],
                "end_user_conditions": [] }
            ],
            "brands": [
              { "id": 9, "url": "https://unit-test.zendesk.com/api/v2/brands/9.json?leak={{Sentinel}}",
                "name": "Acme Support", "subdomain": "acme", "active": true, "default": true,
                "has_help_center": true, "signature_template": "{{Sentinel}} signature",
                "logo": { "id": 77, "content_url": "https://cdn.example/{{Sentinel}}-logo.png",
                          "thumbnails": [ { "content_url": "{{Sentinel}}" } ] } }
            ],
            "custom_statuses": [
              { "id": 1001, "url": "https://unit-test.zendesk.com/api/v2/custom_statuses/1001.json?leak={{Sentinel}}",
                "status_category": "hold", "agent_label": "Awaiting vendor", "active": true,
                "raw_agent_label": "{{Sentinel}} raw", "end_user_label": "On hold",
                "raw_end_user_label": "{{Sentinel}} raw", "description": "{{Sentinel}} desc" }
            ],
            "suspended_tickets": [
              { "id": 3436, "url": "https://unit-test.zendesk.com/api/v2/suspended_tickets/3436.json?leak={{Sentinel}}",
                "subject": "Help!", "cause": "Detected as spam",
                "author": { "id": 9, "name": "Sender", "email": "sender@example.org" },
                "brand_id": 9, "ticket_id": 101, "created_at": "2026-07-01T00:00:00Z",
                "content": "{{Sentinel}} full RAW inbound email with headers", "via": { "channel": "email" } }
            ],
            "identities": [
              { "id": 501, "url": "https://unit-test.zendesk.com/api/v2/users/201/identities/501.json?leak={{Sentinel}}",
                "user_id": 201, "type": "email", "value": "sam@example.org", "primary": true,
                "verified": true, "deliverable_state": "{{Sentinel}} deliverable" }
            ],
            "attachments": [
              { "id": 601, "url": "https://unit-test.zendesk.com/api/v2/attachments/601.json?leak={{Sentinel}}",
                "file_name": "error.log", "content_type": "text/plain", "size": 2048, "inline": false,
                "malware_scan_result": "malware_not_found",
                "content_url": "https://unit-test.zendesk.com/attachments/{{Sentinel}}",
                "mapped_content_url": "https://unit-test.zendesk.com/attachments/{{Sentinel}}",
                "thumbnails": [ { "content_url": "https://unit-test.zendesk.com/attachments/{{Sentinel}}-thumb" } ] }
            ],
            "side_conversations": [
              { "id": "c6d0c5f8", "url": "https://unit-test.zendesk.com/api/v2/tickets/101/side_conversations/c6d0c5f8?leak={{Sentinel}}",
                "subject": "Vendor escalation", "state": "open", "created_at": "2026-07-01T00:00:00Z",
                "message_added_at": "2026-07-02T00:00:00Z",
                "participants": [ { "user_id": 201, "email": "p@example.org", "name": "{{Sentinel}} participant" } ],
                "preview_text": "{{new string('p', 200)}}{{Sentinel}}",
                "external_ids": { "targetTicketId": "{{Sentinel}}" } }
            ],
            "job_statuses": [
              { "id": "job-1", "url": "https://unit-test.zendesk.com/api/v2/job_statuses/job-1.json?leak={{Sentinel}}",
                "status": "completed", "progress": 2, "total": 2, "message": "Completed",
                "results": [
                  { "id": 1, "index": 0, "success": true, "status": "{{Sentinel}} Updated" },
                  { "id": 2, "index": 1, "error": "TooManyValues", "details": "{{Sentinel}} details", "extra": "{{Sentinel}}" }
                ] }
            ],
            "audits": [
              { "id": 701, "ticket_id": 101, "created_at": "2026-07-01T00:00:00Z", "author_id": -1,
                "metadata": { "system": { "ip_address": "{{Sentinel}}", "latitude": 55.6 }, "custom": {} },
                "via": { "channel": "rule",
                         "source": { "rel": "trigger",
                                     "from": { "id": 98, "title": "Notify requester", "deleted": false },
                                     "to": { "address": "{{Sentinel}}@example.org" } } },
                "events": [
                  { "id": 1, "type": "Change", "field_name": "status", "previous_value": "open",
                    "value": "solved", "url": "https://unit-test.zendesk.com/?leak={{Sentinel}}" },
                  { "id": 2, "type": "Comment", "public": true, "body": "{{Sentinel}} markdown body",
                    "html_body": "<p>{{Sentinel}} html body</p>",
                    "plain_body": "{{new string('x', 200)}}{{Sentinel}}",
                    "attachments": [ { "content_url": "{{Sentinel}}" } ] },
                  { "id": 3, "type": "VoiceComment", "public": true, "body": "{{Sentinel}} call",
                    "data": { "recording_url": "{{Sentinel}}", "transcription_text": "{{Sentinel}} transcript" } }
                ] }
            ],
            "sections": [
              { "id": 42, "url": "https://unit-test.zendesk.com/api/v2/help_center/sections/42.json?leak={{Sentinel}}",
                "html_url": "https://unit-test.zendesk.com/hc/en-us/sections/42", "name": "Billing",
                "description": "{{new string('s', 200)}}{{Sentinel}}", "category_id": 7, "position": 2,
                "updated_at": "2026-06-01T00:00:00Z", "theme_template": "section_page" }
            ],
            "categories": [
              { "id": 7, "url": "https://unit-test.zendesk.com/api/v2/help_center/categories/7.json?leak={{Sentinel}}",
                "html_url": "https://unit-test.zendesk.com/hc/en-us/categories/7", "name": "FAQ",
                "description": "{{new string('c', 200)}}{{Sentinel}}", "position": 1,
                "updated_at": "2026-06-01T00:00:00Z" }
            ],
            "satisfaction_ratings": [
              { "id": 62, "url": "https://unit-test.zendesk.com/api/v2/satisfaction_ratings/62.json?leak={{Sentinel}}",
                "score": "bad", "comment": "Slow response", "reason": "Took too long", "reason_code": 100,
                "reason_id": 5, "ticket_id": 208, "requester_id": 7881, "assignee_id": 135, "group_id": 44,
                "created_at": "2026-07-01T00:00:00Z", "updated_at": "2026-07-02T00:00:00Z" }
            ],
            "deleted_tickets": [
              { "id": 501, "subject": "Deleted ticket",
                "actor": { "id": 7, "name": "Agent Smith" },
                "deleted_at": "2026-07-01T00:00:00Z", "previous_state": "open",
                "url": "https://unit-test.zendesk.com/api/v2/deleted_tickets/501.json?leak={{Sentinel}}",
                "raw_subject": "{{Sentinel}} raw subject" }
            ],
            "custom_objects": [
              { "key": "apartment", "title": "Apartment", "title_pluralized": "Apartments",
                "description": "Rental units", "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-06-01T00:00:00Z",
                "url": "https://unit-test.zendesk.com/api/v2/custom_objects/apartment.json?leak={{Sentinel}}",
                "raw_title": "{{Sentinel}} raw", "raw_description": "{{Sentinel}} raw",
                "raw_title_pluralized": "{{Sentinel}} raw" }
            ],
            "custom_object_records": [
              { "id": "01HXAPARTMENT4B", "name": "Unit 4B", "custom_object_key": "apartment",
                "external_id": "ext-4b", "custom_object_fields": { "beds": 2, "floor": 4 },
                "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-06-01T00:00:00Z",
                "created_by_user_id": 1, "updated_by_user_id": 2,
                "url": "https://unit-test.zendesk.com/api/v2/custom_objects/apartment/records/01HX.json?leak={{Sentinel}}",
                "photo": { "content_url": "https://cdn.example/{{Sentinel}}.png" } }
            ],
            "results": [
              { "result_type": "ticket", "id": 801, "url": "https://unit-test.zendesk.com/api/v2/tickets/801.json?leak={{Sentinel}}",
                "subject": "Search hit", "description": "{{new string('r', 150)}}{{Sentinel}}", "status": "open",
                "custom_fields": [ { "id": 1, "value": "{{Sentinel}}" } ] },
              { "result_type": "user", "id": 802, "name": "Found User", "email": "found@example.org",
                "notes": "{{Sentinel}} notes", "photo": { "content_url": "{{Sentinel}}" } }
            ],
            "group_memberships": [
              { "id": 901, "url": "https://unit-test.zendesk.com/api/v2/group_memberships/901.json?leak={{Sentinel}}",
                "user_id": 201, "group_id": 3, "default": true,
                "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-01-01T00:00:00Z" }
            ],
            "organization_memberships": [
              { "id": 902, "url": "https://unit-test.zendesk.com/api/v2/organization_memberships/902.json?leak={{Sentinel}}",
                "user_id": 201, "organization_id": 301, "default": false,
                "created_at": "2026-01-01T00:00:00Z", "updated_at": "2026-01-01T00:00:00Z" }
            ],
            "tags": [
              { "name": "vip", "count": 10, "url": "https://unit-test.zendesk.com/api/v2/tags/vip.json?leak={{Sentinel}}" }
            ],
            "group_settings": [
              { "id": 1, "setting": "{{Sentinel}} opaque settings blob" }
            ]
          }
          """;

    [Fact]
    public async Task Every_Read_List_Tool_Strips_The_Poisoned_Fixture_And_Emits_The_Lean_Envelope()
    {
        var toolTypes = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .OrderBy(type => type.Name)
            .ToList();

        // The exclusion list must exactly complement the swept set: no stale/typo entries...
        var readToolNames = toolTypes
            .SelectMany(type => ReadToolMethods(type).Select(method => ToolName(method)))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(ExcludedReadTools.Except(readToolNames));

        var failures = new List<string>();
        var sweptTools = 0;

        foreach (var type in toolTypes)
        {
            var sweptMethods = ReadToolMethods(type)
                .Where(method => !ExcludedReadTools.Contains(ToolName(method)))
                .OrderBy(method => ToolName(method), StringComparer.Ordinal)
                .ToList();
            if (sweptMethods.Count == 0) continue;

            var harness = new ZendeskToolHarness();
            var constructor = Assert.Single(type.GetConstructors());
            var tools = constructor.Invoke([
                .. constructor.GetParameters().Select(parameter => CtorArgument(parameter, harness))
            ]);

            foreach (var method in sweptMethods)
            {
                sweptTools++;
                var toolName = ToolName(method);
                var requestsBefore = harness.Requests.Count;
                harness.EnqueueJson(PoisonedResponse);
                try
                {
                    var arguments = method.GetParameters().Select(Argument).ToArray();
                    var task = Assert.IsAssignableFrom<Task>(method.Invoke(tools, arguments));
                    await task;
                    var result = task.GetType().GetProperty("Result")!.GetValue(task);
                    var serialized = JsonSerializer.Serialize(result);

                    // The sweep only means something when the poisoned fixture was actually served.
                    if (harness.Requests.Count == requestsBefore)
                    {
                        failures.Add($"{toolName}: never called Zendesk — the poisoned fixture was not exercised.");
                        continue;
                    }

                    if (serialized.Contains(Sentinel, StringComparison.Ordinal))
                        failures.Add($"{toolName}: leaked poisoned fields into its default response: " +
                                     LeakContext(serialized));

                    using var document = JsonDocument.Parse(serialized);
                    if (document.RootElement.ValueKind is not JsonValueKind.Object ||
                        !document.RootElement.TryGetProperty("items", out var items) ||
                        items.ValueKind is not JsonValueKind.Array)
                        failures.Add($"{toolName}: response does not carry the uniform 'items' list envelope.");
                    else if (!document.RootElement.TryGetProperty("detail", out var detail) ||
                             detail.GetString() != "summary")
                        failures.Add($"{toolName}: default response is not labeled detail:'summary'.");
                }
                catch (Exception exception)
                {
                    var actual = exception is TargetInvocationException { InnerException: { } inner }
                        ? inner
                        : exception;
                    failures.Add($"{toolName}: threw {actual.GetType().Name} instead of projecting the poisoned " +
                                 $"fixture: {actual.Message}");
                }
            }
        }

        Assert.Equal(ExpectedSweptTools, sweptTools);
        Assert.True(failures.Count == 0,
            $"Read tools escaped the ZendeskLean projection gateway:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures));
    }

    /// <summary>The declared read (ReadOnly) tool methods of a tool class.</summary>
    private static IEnumerable<MethodInfo> ReadToolMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is { ReadOnly: true });

    private static string ToolName(MethodInfo method) =>
        method.GetCustomAttribute<McpServerToolAttribute>()!.Name!;

    /// <summary>First leak locations with surrounding context, so a failure names the offending field.</summary>
    private static string LeakContext(string serialized)
    {
        var contexts = new List<string>();
        var index = serialized.IndexOf(Sentinel, StringComparison.Ordinal);
        while (index >= 0 && contexts.Count < 3)
        {
            var start = Math.Max(0, index - 70);
            contexts.Add($"...{serialized[start..Math.Min(serialized.Length, index + Sentinel.Length + 10)]}...");
            index = serialized.IndexOf(Sentinel, index + Sentinel.Length, StringComparison.Ordinal);
        }

        return string.Join(" | ", contexts);
    }

    /// <summary>
    ///     Resolves a read-tool constructor dependency over the shared wire harness — the same object graph
    ///     production DI builds, so the sweep exercises the real request/projection path.
    /// </summary>
    private static object CtorArgument(ParameterInfo parameter, ZendeskToolHarness harness)
    {
        var type = parameter.ParameterType;
        if (type == typeof(ZendeskSupportApiClient)) return harness.CreateSupportClient();
        if (type == typeof(ZendeskHelpCenterApiClient)) return harness.CreateHelpCenterClient();
        if (type == typeof(IRequestAdapter)) return harness.CreateAdapter();
        if (type == typeof(IOptionsMonitor<McpOptions>)) return new StaticOptionsMonitor<McpOptions>(new McpOptions());
        throw new InvalidOperationException(
            $"Unsupported read-tool constructor dependency '{type.Name}' — teach the sweep how to build it.");
    }

    /// <summary>
    ///     Builds a tool argument that lets the call reach the wire with its DEFAULT response shape: declared
    ///     parameter defaults are honored (that is what "default response" means), and the handful of required /
    ///     mutually-exclusive parameters get minimal valid values by name.
    /// </summary>
    private static object? Argument(ParameterInfo parameter)
    {
        if (parameter.ParameterType == typeof(CancellationToken)) return TestContext.Current.CancellationToken;
        return parameter.Name switch
        {
            "query" => "status:open", // search tools require a non-blank query
            "name" => "Acme", // autocomplete/lookup tools require a non-blank name
            "externalId" when !parameter.HasDefaultValue => "ext-1", // tickets_get_by_external_id
            "startTime" => 1_700_000_000L, // incremental export: exactly one of startTime/cursor
            "ids" when parameter.ParameterType == typeof(long[]) => new long[] { 1, 2 },
            "ids" when parameter.ParameterType == typeof(string[]) => new[] { "job-1" },
            _ when parameter.HasDefaultValue => parameter.DefaultValue,
            _ when parameter.ParameterType == typeof(long) => 1L,
            _ when parameter.ParameterType == typeof(int) => 1,
            _ when parameter.ParameterType == typeof(string) => "value",
            _ when parameter.ParameterType.IsArray =>
                Array.CreateInstance(parameter.ParameterType.GetElementType()!, 0),
            _ when Nullable.GetUnderlyingType(parameter.ParameterType) is not null => null,
            _ => Activator.CreateInstance(parameter.ParameterType)
        };
    }
}