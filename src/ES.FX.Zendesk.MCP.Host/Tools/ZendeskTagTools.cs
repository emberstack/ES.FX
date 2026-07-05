using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk tags (account-wide tag usage). Namespaced <c>tags_*</c>.
/// </summary>
/// <remarks>
///     Every endpoint reads the raw wire JSON via <see cref="ZendeskKiotaRequests.SendForJsonAsync" />: the
///     generated models declare their payload fields <c>readOnly</c> (<c>TagListTagObject.count</c>, the
///     offset-paging <c>count</c>/<c>next_page</c>/<c>previous_page</c> on <c>TagsResponse</c>, and both fields
///     of <c>TagCountObject</c>), so re-serializing them through Kiota would silently drop those fields.
///     <c>tags_list</c> is then wrapped in the uniform <see cref="ZendeskLean" /> envelope — which replaces the
///     absolute <c>next_page</c>/<c>previous_page</c> URL strings and cursor <c>links</c> blocks with lean
///     continuation metadata — while <c>tags_count</c>/<c>tags_autocomplete</c> pass through unchanged (their
///     payloads are already minimal). The list endpoint additionally needs the
///     <see cref="ZendeskKiotaRequests.WithCursorPagination" /> escape hatch (the generated builder only exposes
///     offset paging).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskTagTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The explicit default page size, applied to whichever paging regime a call uses.</summary>
    private const int DefaultPageSize = 50;

    /// <summary>Lists the most popular Zendesk tags with usage counts.</summary>
    [McpServerTool(Name = "tags_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Most popular tags of last 60 days with usage counts (up to 20,000, decreasing popularity; own rate " +
        "limit). Rows {name,count} — already minimal, so detail summary=full. Prefer cursor paging " +
        "(pageSize/afterCursor; response has_more/after_cursor drive continuation); offset paging (page/perPage; " +
        "response next_page is next page NUMBER) capped at 10,000 records, leaving the tail unreachable. Default " +
        "page size 50 either regime. Exact spelling of one tag: tags_autocomplete; account-wide total: " +
        "tags_count.")]
    public Task<JsonElement> List(
        [Description(
            "1-based offset page number (capped at 10,000 records). Ignored when pageSize/afterCursor select the " +
            "cursor regime — regimes never mix on the wire.")]
        int? page = null,
        [Description(
            "Offset results per page (default 50 when offset paging used, max 100). Ignored when " +
            "pageSize/afterCursor select the cursor regime.")]
        int? perPage = null,
        [Description("Cursor page size (default 50, max 100 per page). Preferred over page/perPage.")]
        int? pageSize = null,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for first page. When present must be the opaque token " +
            "copied verbatim from the previous response's after_cursor; not a page number, must not be guessed " +
            "or passed empty (invalid cursor → 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(
            "Row detail: summary (default) or full — tag rows {name,count} already minimal, so both return the " +
            "same rows.")]
        string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The endpoint supports both Zendesk paging regimes; the envelope reports the one the call used.
            // The default page size is explicit on the wire in whichever regime applies — never left to
            // Zendesk's server default of 100.
            var usesOffsetPaging = (page is not null || perPage is not null) &&
                                   pageSize is null && string.IsNullOrWhiteSpace(afterCursor);
            if (usesOffsetPaging)
            {
                perPage ??= DefaultPageSize;
            }
            else
            {
                // The regimes are mutually exclusive on the wire (the OAS DualPaginationPage parameter: "use one
                // format or the other, not both") — cursor params win, conflicting page/perPage are dropped.
                page = null;
                perPage = null;
                pageSize ??= DefaultPageSize;
            }

            var request = zendesk.Api.V2.Tags.ToGetRequestInformation(configuration =>
                {
                    configuration.QueryParameters.Page = page;
                    configuration.QueryParameters.PerPage = perPage;
                })
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return BuildTagsEnvelope(json, usesOffsetPaging, page, parsedDetail, MaxResponseChars("tags_list"));
        });

    /// <summary>Returns the account-wide tag count.</summary>
    [McpServerTool(Name = "tags_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Cached, approximate account-wide tag count; refreshed_at = when cached value was computed. Once true " +
        "count exceeds 100,000 the value refreshes only every 24h and stays capped at 100,000 until that " +
        "background update completes, during which refreshed_at may be null.")]
    public Task<JsonElement> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Tags.Count.ToGetRequestInformation();
            var response = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // Unwrap the { "count": { value, refreshed_at } } envelope, as before.
            return response.ValueKind is JsonValueKind.Object
                   && response.TryGetProperty("count", out var count)
                   && count.ValueKind is JsonValueKind.Object
                ? count
                : throw new InvalidOperationException("Zendesk returned no tag count.");
        });

    /// <summary>Suggests Zendesk tag names matching a prefix.</summary>
    [McpServerTool(Name = "tags_autocomplete", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Suggests Zendesk tag names matching a prefix (minimum two characters; up to 15 suggestions drawn ONLY " +
        "from the most-used ticket tags of the last 60 days — a tag that matches the prefix but is outside that top " +
        "set will not appear). Use to find the exact spelling of a tag before searching or tagging. Read-only.")]
    public Task<JsonElement> Autocomplete(
        [Description(
            "The tag name prefix to complete (minimum 2 characters). Each word within a tag is indexed separately " +
            "(split on underscores, hyphens, spaces, or other punctuation), so a tag matches if the tag itself OR " +
            "any word within it starts with the prefix (e.g. \"trig\" matches \"set_by_this_trigger\" via the word " +
            "\"trigger\").")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Autocomplete.Tags.ToGetRequestInformation(configuration =>
                configuration.QueryParameters.Name = name);
            var response = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // The plain string-array envelope ({ "tags": [ "name", ... ] }) always carried an array, even when
            // Zendesk omitted the property.
            if (response.ValueKind is JsonValueKind.Object && response.TryGetProperty("tags", out _))
                return response;
            var patched = response.ValueKind is JsonValueKind.Object
                ? JsonNode.Parse(response.GetRawText())!.AsObject()
                : new JsonObject();
            patched["tags"] = new JsonArray();
            return JsonSerializer.SerializeToElement(patched);
        });
    }

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Builds the lean list envelope for <c>tags_list</c>, in the pagination regime the call used. Tag rows
    ///     ({name, count}) are already minimal, so <c>summary</c> and <c>full</c> return identical rows; the
    ///     envelope is built in full view (<see cref="ZendeskLean" /> registers no <c>tags</c> summary shape —
    ///     there is nothing to strip row-wise) and the <c>detail</c> label is then rewritten to the caller's
    ///     validated value. Summary mode still owes the envelope's sideload contract — any sideloaded array is
    ///     summary-projected (or omitted with a note) via
    ///     <see cref="ZendeskLean.ApplySummarySideloadContract" /> before the full-mode assembly, so a raw
    ///     Zendesk sideload can never ride a summary response in full view. What the envelope DOES strip is the
    ///     raw response's absolute <c>next_page</c>/<c>previous_page</c> URL strings and cursor <c>links</c>
    ///     blocks, replaced by the uniform continuation metadata.
    /// </summary>
    private static JsonElement BuildTagsEnvelope(JsonElement response, bool usesOffsetPaging, int? requestPage,
        ZendeskDetail detail, int maxResponseChars)
    {
        string? note = null;
        if (detail is ZendeskDetail.Summary && response.ValueKind is JsonValueKind.Object &&
            JsonNode.Parse(response.GetRawText()) is JsonObject source)
        {
            note = ZendeskLean.ApplySummarySideloadContract(source, "tags");
            response = JsonSerializer.SerializeToElement(source);
        }

        var envelope = usesOffsetPaging
            ? ZendeskLean.BuildOffsetListEnvelope(response, "tags", requestPage, ZendeskDetail.Full,
                maxResponseChars, note)
            : ZendeskLean.BuildCursorListEnvelope(response, "tags", ZendeskDetail.Full, maxResponseChars, note);
        if (detail is ZendeskDetail.Full) return envelope;

        var patched = (JsonObject)JsonNode.Parse(envelope.GetRawText())!;
        patched["detail"] = "summary";
        return JsonSerializer.SerializeToElement(patched);
    }
}