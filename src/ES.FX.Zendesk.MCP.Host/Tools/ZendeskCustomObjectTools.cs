using System.ComponentModel;
using System.Text.Json;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk custom objects — the tenant-defined business data (orders, assets,
///     subscriptions, …) that can be linked to tickets and users. Namespaced <c>custom_objects_*</c>.
/// </summary>
/// <remarks>
///     Responses are parsed as raw wire JSON via <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> and projected
///     through <see cref="ZendeskLean" />: object-type metadata drops the raw_* localization duplicates; record
///     rows keep their <c>custom_object_fields</c> map (the business data) and strip only the self-link and photo.
///     Record ids are opaque strings (ULIDs), not integers. Records use cursor pagination; the object-type list is
///     unpaginated (tenants define few object types).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskCustomObjectTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API " +
        "links).";

    /// <summary>The uniform cursor-continuation parameter description shared by the record list/search tools.</summary>
    private const string AfterCursorDescription =
        "Continuation cursor for the NEXT page — OMIT for first page. Opaque token copied verbatim from the " +
        "previous response's after_cursor; not a page number, don't guess or pass empty (invalid cursor → 400).";

    /// <summary>Lists the tenant's custom object types.</summary>
    [McpServerTool(Name = "custom_objects_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the tenant's custom object TYPES (e.g. \"order\", \"asset\", \"apartment\") — start here to discover " +
        "the object 'key' you pass to the custom_objects_records_* tools. Rows are lean summaries (key, title, " +
        "title_pluralized, description, dates). Feature-gated: absent/empty when the tenant defines no custom " +
        "objects. Read-only.")]
    public Task<JsonElement> List(
        CancellationToken cancellationToken,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Custom_objects.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "custom_objects", null, parsedDetail,
                MaxResponseChars("custom_objects_list"));
        });

    /// <summary>Lists records of a custom object type.</summary>
    [McpServerTool(Name = "custom_objects_records_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists RECORDS of a custom object type (the business rows: orders, assets, …) as lean summaries including " +
        "the custom_object_fields map (the actual data). Optionally filter to specific external_ids (e.g. the " +
        "record external_id a ticket references). Use custom_objects_records_search for free-text search, " +
        "custom_objects_records_get for one record. Cursor pagination: pageSize default 25 (max 100); response " +
        "has_more/after_cursor drive continuation. Read-only.")]
    public Task<JsonElement> RecordsList(
        [Description("The custom object type key (from custom_objects_list), e.g. \"order\".")]
        string customObjectKey,
        [Description(
            "Restrict to records with these external ids — comma-separated (e.g. \"ext-4b,ext-9c\"). Optional.")]
        string? externalIds = null,
        [Description("Cursor page size (default 25, max 100).")]
        int? pageSize = 25,
        [Description(AfterCursorDescription)] string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(customObjectKey))
                throw new McpException("Provide a custom object type key (see custom_objects_list).");
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Custom_objects[customObjectKey].Records
                .ToGetRequestInformation(configuration =>
                {
                    if (!string.IsNullOrWhiteSpace(externalIds))
                        configuration.QueryParameters.FilterexternalIds = externalIds;
                    configuration.QueryParameters.Pagesize = pageSize;
                    if (!string.IsNullOrWhiteSpace(afterCursor)) configuration.QueryParameters.Pageafter = afterCursor;
                });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "custom_object_records", parsedDetail,
                MaxResponseChars("custom_objects_records_list"));
        });

    /// <summary>Full-text searches records of a custom object type.</summary>
    [McpServerTool(Name = "custom_objects_records_search", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Free-text search RECORDS of a custom object type — find the business record (order, asset, subscription) " +
        "linked to a ticket/user while investigating. Matches record name and field values. Rows are lean " +
        "summaries including the custom_object_fields map. Cursor pagination: pageSize default 25 (max 100); " +
        "has_more/after_cursor drive continuation. Read-only.")]
    public Task<JsonElement> RecordsSearch(
        [Description("The custom object type key (from custom_objects_list), e.g. \"order\".")]
        string customObjectKey,
        [Description("Search text; matches record name and field values.")]
        string query,
        [Description("Cursor page size (default 25, max 100).")]
        int? pageSize = 25,
        [Description(AfterCursorDescription)] string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(customObjectKey))
                throw new McpException("Provide a custom object type key (see custom_objects_list).");
            if (string.IsNullOrWhiteSpace(query)) throw new McpException("Provide a non-blank query.");
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Custom_objects[customObjectKey].Records.Search
                .ToGetRequestInformation(configuration =>
                {
                    configuration.QueryParameters.Query = query;
                    configuration.QueryParameters.Pagesize = pageSize;
                    if (!string.IsNullOrWhiteSpace(afterCursor)) configuration.QueryParameters.Pageafter = afterCursor;
                });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "custom_object_records", parsedDetail,
                MaxResponseChars("custom_objects_records_search"));
        });

    /// <summary>Returns a single custom object record by id.</summary>
    [McpServerTool(Name = "custom_objects_records_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single custom object record by id — the detail sink for the custom_objects_records_* rows, including the " +
        "full custom_object_fields map. Record ids are opaque strings (ULIDs), not numbers. Null fields and API " +
        "self-links omitted; absent field means null/empty. Read-only.")]
    public Task<JsonElement> RecordRead(
        [Description("The custom object type key (from custom_objects_list), e.g. \"order\".")]
        string customObjectKey,
        [Description("The record id — an opaque string/ULID (from a custom_objects_records_* row's id).")]
        string recordId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(customObjectKey))
                throw new McpException("Provide a custom object type key (see custom_objects_list).");
            if (string.IsNullOrWhiteSpace(recordId)) throw new McpException("Provide a record id.");
            var request = zendesk.Api.V2.Custom_objects[customObjectKey].Records[recordId].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object &&
                   json.TryGetProperty("custom_object_record", out var record)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(record), "custom_objects_records_get",
                    MaxResponseChars("custom_objects_records_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk custom object record '{recordId}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}