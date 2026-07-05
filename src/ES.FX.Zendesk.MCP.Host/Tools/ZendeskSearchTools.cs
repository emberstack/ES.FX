using System.ComponentModel;
using ES.FX.Zendesk.Support;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for the Zendesk unified search API. Namespaced <c>search_*</c>. The unified count is the only
///     genuinely cross-resource operation kept in the <c>search</c> area; the ticket-scoped deep export lives on
///     <see cref="ZendeskTicketTools" /> as <c>tickets_search_export</c> (its name says <c>tickets</c>, so its area
///     is <c>tickets</c> — keeping the class area-homogeneous for area gating).
/// </summary>
[McpServerToolType]
public sealed class ZendeskSearchTools(ZendeskSupportApiClient zendesk)
{
    /// <summary>Returns the number of results a search query matches.</summary>
    [McpServerTool(Name = "search_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Returns the number of results a Zendesk search query matches — a cheap way to size a query before paging " +
        "or exporting. Uses the same query syntax as tickets_search. Read-only.")]
    public Task<long> Count(
        [Description(
            "The Zendesk search query. Combine keyword:value terms with spaces (implicit AND); repeat a keyword " +
            "to OR its values (tags:a tags:b = a OR b); prefix a term with '-' to exclude; use '*' as a suffix " +
            "wildcard (subject:photo*); quote multi-word values (\"upgrade account\"); no spaces around operators " +
            "(status:open, not status: open). Comparison operators ':' (equals), '<' '>' '<=' '>=' — meaningful " +
            "on the ordered enums status and priority (priority>=high, status<solved). Restrict result type with " +
            "type: (ticket, user, organization, group). Common TICKET keywords: status " +
            "(new/open/pending/hold/solved/closed), priority (low/normal/high/urgent), ticket_type " +
            "(question/incident/problem/task), subject/description/comment (free text), tags (tag name or 'none'; " +
            "quote multiple to require ALL), has_attachment (true/false), recipient, via (source channel — email " +
            "is via:mail NOT via:email; also web, api, chat, phone, sms, voicemail, twitter, facebook, " +
            "side_conversation, or 'none'), form:\"<form name>\", custom_field_<id>:<value>, fieldvalue:<value>. " +
            "People keywords assignee/requester/submitter/cc/commenter accept a user id, name, email, or phone, " +
            "plus 'me' and 'none'; group and organization accept a NAME or id (or 'none'); brand accepts a name " +
            "or id. Date keywords created/updated/solved/due_date take YYYY-MM-DD, ISO8601 " +
            "(created>2015-09-01T12:00:00-08:00), or relative amounts attached to the unit (created>4hours, " +
            "updated<7days; units minutes/hours/days/weeks/months/years). For type:user: name, email, role " +
            "(end-user/agent/admin), organization, tags, notes, phone, external_id, is_verified, is_suspended. " +
            "For type:organization: name, tags, notes, details, created, external_id. Example: \"type:ticket " +
            "status:open priority>=high assignee:none via:mail created>2days\".")]
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var response = await zendesk.Api.V2.Search.Count
                .GetAsync(configuration => configuration.QueryParameters.Query = query, cancellationToken)
                .ConfigureAwait(false);
            // /search/count returns a plain integer envelope ({ "count": N }), unlike the
            // { value, refreshed_at } count records elsewhere — surface the bare number, as before.
            return (long)(response?.Count ?? 0);
        });
    }
}