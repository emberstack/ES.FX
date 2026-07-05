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
///     MCP read tools for Zendesk job statuses — the async jobs returned by bulk operations. Namespaced
///     <c>job_statuses_*</c>.
/// </summary>
/// <remarks>
///     The generated job-status models (<c>JobStatusObject</c>/<c>JobStatusResultObject</c>) declare every field
///     <c>readOnly</c> in the spec, so Kiota parses them into properties its serializer never writes back —
///     re-serializing a response model would drop the entire payload. These tools therefore read the raw wire
///     JSON via <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> and project it through
///     <see cref="ZendeskLean" />: summary rows collapse the heavy per-item <c>results</c> array to
///     <c>results_summary</c> (succeeded/failed counts plus the first failures), and <c>detail:'full'</c>
///     reaches the complete records. <c>job_statuses_get</c> is a deliberate exception to the
///     <c>*_get</c>-as-full-sink rule: a completed bulk job's results array can dwarf the answer an agent
///     polls for, so it defaults to the lean-by-status shape too. The list endpoint additionally needs the
///     <see cref="ZendeskKiotaRequests.WithCursorPagination" /> escape hatch (the generated builder only exposes
///     a single opaque <c>page</c> parameter, not <c>page[size]</c>/<c>page[after]</c>).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskJobStatusTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c> id lists.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean status rows; per-item results collapse to results_summary) or " +
        "'full' (complete records minus null fields and API links, incl. full per-item results array).";

    /// <summary>Lists recent Zendesk job statuses.</summary>
    [McpServerTool(Name = "job_statuses_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Recent job statuses (async jobs from bulk write tools), sorted by completion date then creation date " +
        "desc, as lean status rows: id, status, progress, total, failure message, results_summary " +
        "(succeeded/failed counts + first 5 failures) instead of full per-item results — pass detail:'full', or " +
        "job_statuses_get(_many), for complete records. Data retained ~1 day. Cursor pagination only; response " +
        "has_more/after_cursor drive continuation.")]
    public Task<JsonElement> List(
        [Description("Results per page (default 20, max 100).")]
        int? pageSize = 20,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for first page. When present, the opaque token copied " +
            "verbatim from prior response's after_cursor; not a page number, don't guess or pass empty (invalid " +
            "cursor → 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Job_statuses.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "job_statuses", parsedDetail,
                MaxResponseChars("job_statuses_list"));
        });

    /// <summary>Returns a Zendesk job status by id.</summary>
    [McpServerTool(Name = "job_statuses_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single job status by string id. Poll after bulk write tools (which return a job_status) until status is " +
        "completed or failed. Lean by status by default: queued/working carry {id,status,progress,total}; " +
        "completed adds results_summary (succeeded/failed counts + first 5 failures); failed adds failure " +
        "message. detail:'full' for complete record incl. full per-item results array (job_statuses_get_many " +
        "with detail:'full' does this for several jobs at once). Data retained ~1 day.")]
    public Task<JsonElement> Read(
        [Description("Job status id — opaque string (not numeric), returned by the bulk operation that started " +
                     "the job.")]
        string id,
        CancellationToken cancellationToken = default,
        [Description(
            "Detail: 'summary' (default — lean by status: id/status/progress/total, plus results_summary or " +
            "failure message) or 'full' (complete job status incl. full per-item results array).")]
        string detail = "summary")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Job_statuses[id].ToGetRequestInformation();
            var response = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.ValueKind is not JsonValueKind.Object ||
                !response.TryGetProperty("job_status", out var jobStatus) ||
                jobStatus.ValueKind is not JsonValueKind.Object)
                throw new McpException($"Zendesk job status '{id}' was not found.");
            return parsedDetail is ZendeskDetail.Full
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(jobStatus), "job_statuses_get",
                    MaxResponseChars("job_statuses_get"), "Record exceeds the response budget.")
                : SummarizeJobStatus(jobStatus);
        });
    }

    /// <summary>Returns many Zendesk job statuses in one request.</summary>
    [McpServerTool(Name = "job_statuses_get_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Up to 100 job statuses in one request — prefer over repeated job_statuses_get when polling several " +
        "bulk jobs. Hard cap 100 ids/call (Zendesk show_many limit); for more, split ids into batches of 100, " +
        "one call per batch. Rows are lean status summaries (per-item results collapse to results_summary) — " +
        "detail:'full' for complete records incl. full per-item results arrays (complete failure enumeration " +
        "lives here).")]
    public Task<JsonElement> ReadMany(
        [Description("Job status ids — opaque strings (not numeric); at most 100 per call.")]
        string[] ids,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // show_many rejects more than 100 ids with 400 Bad Request — surface the API contract as an
            // actionable batching instruction instead of silently fanning out server-side.
            if (ids.Length > MaxIdsPerShowManyRequest)
                throw new McpException(
                    $"job_statuses_get_many accepts at most {MaxIdsPerShowManyRequest} ids per call (Zendesk's " +
                    $"show_many limit) but was passed {ids.Length}. Split the ids into batches of " +
                    $"{MaxIdsPerShowManyRequest} and call once per batch.");
            if (ids.Length == 0)
                return ZendeskLean.BuildOffsetListEnvelope(EmptyJobStatusesResponse(), "job_statuses",
                    null, parsedDetail, MaxResponseChars("job_statuses_get_many"));

            var request = zendesk.Api.V2.Job_statuses.Show_many.ToGetRequestInformation(configuration =>
                configuration.QueryParameters.Ids = string.Join(',', ids));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "job_statuses", null, parsedDetail,
                MaxResponseChars("job_statuses_get_many"));
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     Projects a single job status through its <see cref="ZendeskLean" /> summary shape — lean by status:
    ///     absent fields simply do not appear, so queued/working jobs carry only id/status/progress/total while
    ///     completed/failed ones add results_summary and the failure message.
    /// </summary>
    private static JsonElement SummarizeJobStatus(JsonElement jobStatus)
    {
        var entity = (JsonObject)JsonNode.Parse(jobStatus.GetRawText())!;
        return JsonSerializer.SerializeToElement(ZendeskLean.SummarizeEntity("job_statuses", entity)!);
    }

    /// <summary>An empty wire-shaped job-statuses response, for the no-ids fast path of <c>job_statuses_get_many</c>.</summary>
    private static JsonElement EmptyJobStatusesResponse() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["job_statuses"] = new JsonArray() });
}