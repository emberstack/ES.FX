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
///     MCP read tools for Zendesk CSAT satisfaction ratings (the customer's good/bad rating of a solved ticket,
///     with an optional comment and reason). Namespaced <c>satisfaction_ratings_*</c>.
/// </summary>
/// <remarks>
///     Responses are parsed as raw wire JSON via <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> rather than
///     the generated models, matching the other read tools: the generated request builders are used to keep path
///     templating/encoding intact, but the <c>score</c>/<c>start_time</c>/<c>end_time</c> filters the live API
///     accepts are missing from the generated query parameters and are added via the
///     <see cref="ZendeskKiotaRequests" /> escape hatch (spec-anomaly ledger,
///     <c>src/ES.FX.Zendesk/OpenApi/README.md</c>). The list response is projected through
///     <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default, complete records via
///     <c>detail:'full'</c> or <c>satisfaction_ratings_get</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskSatisfactionRatingTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API " +
        "links).";

    /// <summary>Lists CSAT satisfaction ratings.</summary>
    [McpServerTool(Name = "satisfaction_ratings_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Customer satisfaction (CSAT) ratings as lean summary rows: id, score, comment, reason, reason_code, " +
        "ticket_id, requester/assignee/group ids, dates. Audit service quality — surface bad ratings with " +
        "comments, or summarize an agent's/customer's CSAT history. satisfaction_ratings_get for one; " +
        "satisfaction_ratings_count for just the number. Admin scope; CSAT-enabled accounts only. perPage default " +
        "25 (max 100); total in 'count'; 'has_more'/'next_page' drive paging.")]
    public Task<JsonElement> List(
        [Description(
            "Filter by score. Unqualified good|bad|offered|received include with+without comment; qualified: " +
            "good_with_comment|good_without_comment|bad_with_comment|bad_without_comment|received_with_comment|" +
            "received_without_comment|unoffered.")]
        string? score = null,
        [Description("Only ratings at/after this Unix epoch time (seconds).")]
        long? startTime = null,
        [Description("Only ratings at/before this Unix epoch time (seconds).")]
        long? endTime = null,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Satisfaction_ratings.ToGetRequestInformation(configuration =>
                {
                    configuration.QueryParameters.Page = page;
                    configuration.QueryParameters.PerPage = perPage;
                })
                // score/start_time/end_time are documented filters the generated builder does not model
                // (spec-anomaly ledger, src/ES.FX.Zendesk/OpenApi/README.md).
                .WithQuery("score", score)
                .WithQuery("start_time", startTime)
                .WithQuery("end_time", endTime);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "satisfaction_ratings", page, parsedDetail,
                MaxResponseChars("satisfaction_ratings_list"));
        });

    /// <summary>Returns a single CSAT satisfaction rating by id.</summary>
    [McpServerTool(Name = "satisfaction_ratings_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single CSAT satisfaction rating by id — the detail sink for satisfaction_ratings_list rows. Null fields " +
        "and API self-links omitted; absent field means null/empty.")]
    public Task<JsonElement> Read(
        [Description("Numeric satisfaction rating id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Satisfaction_ratings[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object &&
                   json.TryGetProperty("satisfaction_rating", out var rating)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(rating), "satisfaction_ratings_get",
                    MaxResponseChars("satisfaction_ratings_get"), "Record exceeds the response budget.")
                : throw new McpException($"Zendesk satisfaction rating '{id}' was not found.");
        });

    /// <summary>Returns the count of CSAT satisfaction ratings.</summary>
    [McpServerTool(Name = "satisfaction_ratings_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Count of CSAT satisfaction ratings — cheaper than listing. Large accounts: the value is approximate and " +
        "may be cached. Admin scope; CSAT-enabled accounts only.")]
    public Task<JsonElement> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Satisfaction_ratings.Count.ToGetRequestInformation();
            return await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}