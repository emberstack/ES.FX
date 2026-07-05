using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk brands (multibrand accounts). Namespaced <c>brands_*</c>.
/// </summary>
/// <remarks>
///     Requests are built through the generated request builders, but responses are returned as the raw wire
///     JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) rather than round-tripped through the
///     generated models: the published spec marks server-assigned fields (brand <c>id</c>, <c>url</c>,
///     <c>created_at</c>/<c>updated_at</c>, <c>ticket_form_ids</c>, ...) as read-only, so Kiota's serializer
///     would silently drop them from the tool result. List responses are then projected through
///     <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default, complete records
///     via <c>detail:'full'</c> or <c>brands_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskBrandTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description for the list tool.</summary>
    private const string DetailDescription =
        "'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API links).";

    /// <summary>Lists Zendesk brands.</summary>
    [McpServerTool(Name = "brands_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Zendesk brands as lean summary rows (id, name, subdomain, active, default, has_help_center; logo and " +
        "signature template stripped); decodes the brand_id on tickets in multibrand accounts. detail:'full' " +
        "for complete records, or brands_get for one. Listing all brands requires admin, or an agent with the " +
        "assign_tickets_to_any_brand permission; agents without it see only brands they are members of. Cursor " +
        "pagination (default pageSize 25 (max 100)); response has_more/after_cursor drive continuation. Read-only.")]
    public Task<JsonElement> List(
        [Description("Results per page (default 25; Zendesk caps at 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page; OMIT for the first page. Opaque token copied verbatim from " +
            "the previous response's after_cursor; not a page number, must not be guessed or passed empty " +
            "(invalid cursor rejected with 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Generator (not spec) gap: the OAS models cursor paging on brands as a oneOf(integer | cursor
            // object) deepObject 'page' parameter, but Kiota collapses it to a scalar-only 'page' property, so
            // page[size]/page[after] go on through the escape hatch.
            var request = zendesk.Api.V2.Brands.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "brands", parsedDetail,
                MaxResponseChars("brands_list"));
        });

    /// <summary>Returns a Zendesk brand by id.</summary>
    [McpServerTool(Name = "brands_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single Zendesk brand by id: full-detail record (null fields and API self-links omitted; absent field " +
        "means null/empty). The name/subdomain behind a ticket's brand_id, plus host mapping, brand URL, and " +
        "signature template. Logo keeps identity fields (file_name, content_url) but its nested per-size " +
        "thumbnails are stripped. Read-only.")]
    public Task<JsonElement> Read(
        [Description("The numeric Zendesk brand id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Brands[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("brand", out var brand)
                ? ZendeskLean.EnsureWithinBudget(StripLogoThumbnails(ZendeskLean.ToFullView(brand)), "brands_get",
                    MaxResponseChars("brands_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk brand '{id}' was not found.");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Drops the <c>thumbnails</c> array nested in a brand's <c>logo</c> — one full attachment object per
    ///     thumbnail size, pure token weight. The logo itself (file_name, content_url, size) stays on the full
    ///     view.
    /// </summary>
    private static JsonElement StripLogoThumbnails(JsonElement brand)
    {
        if (brand.ValueKind is not JsonValueKind.Object) return brand;
        var source = (JsonObject)JsonNode.Parse(brand.GetRawText())!;
        if (source["logo"] is not JsonObject logo || !logo.ContainsKey("thumbnails")) return brand;
        logo.Remove("thumbnails");
        return JsonSerializer.SerializeToElement(source);
    }
}