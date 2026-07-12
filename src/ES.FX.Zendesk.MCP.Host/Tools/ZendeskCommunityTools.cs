using System.ComponentModel;
using System.Text.Json;
using ES.FX.Zendesk.HelpCenter;
using ES.FX.Zendesk.MCP.Host.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for the Zendesk Help Center community (Gather) — peer discussion posts. Namespaced
///     <c>community_*</c>.
/// </summary>
/// <remarks>
///     Like the article tools, responses are parsed as raw wire JSON via
///     <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> (the generated Help Center models type ids as 32-bit
///     integers, which overflow on real 12–13 digit ids) and the offset-pagination parameters the live endpoint
///     accepts but the spec omits are added through the <see cref="ZendeskKiotaRequests" /> escape hatches. The
///     result is projected through <see cref="ZendeskLean" /> into the uniform lean envelope — the post body
///     (<c>details</c>) is stripped from summary rows; read the full post at its <c>html_url</c>.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskCommunityTools(
    ZendeskHelpCenterApiClient helpCenter,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Full-text searches Help Center community (Gather) posts.</summary>
    [McpServerTool(Name = "community_posts_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Full-text search Help Center COMMUNITY (Gather) posts — peer discussions, workarounds and answers that " +
        "never became official KB articles. Complements articles_search / articles_deflection_search when the " +
        "official KB has no answer. Rows are lean summaries (id, title, html_url, status, author_id, topic_id, " +
        "dates, comment_count, vote_sum, pinned, closed); the post body is omitted — read it at html_url. " +
        "Relevance-ranked by default. perPage default 25 (max 25; up to 1000 results total); 'has_more'/'next_page' " +
        "drive paging. Read-only.")]
    public Task<JsonElement> Search(
        [Description("Question/keywords; matches post title and body.")]
        string query,
        [Description("Restrict to a community topic id (see the topic_id on rows).")]
        long? topicId = null,
        [Description("Sort field: \"created_at\" | \"updated_at\". OMIT to keep the default relevance ranking.")]
        string? sortBy = null,
        [Description("asc|desc.")] string? sortOrder = null,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 25). 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(
            "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and " +
            "API links).")]
        string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query)) throw new McpException("Provide a non-blank query.");
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = helpCenter.Api.V2.Help_center.Community_posts.SearchJson
                .ToGetRequestInformation(configuration =>
                {
                    configuration.QueryParameters.Query = query;
                    if (!string.IsNullOrWhiteSpace(sortBy)) configuration.QueryParameters.SortBy = sortBy;
                    if (!string.IsNullOrWhiteSpace(sortOrder)) configuration.QueryParameters.SortOrder = sortOrder;
                })
                // topic (int64), page and per_page are accepted by the live endpoint but the generated builder
                // either types topic as int or omits paging entirely — supply them via the escape hatch
                // (spec-anomaly ledger, src/ES.FX.Zendesk/OpenApi/README.md).
                .WithQuery("topic", topicId)
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // Help Center community search returns posts in a 'results' array; project them onto the post summary.
            return ZendeskLean.BuildOffsetListEnvelope(json, "results", page, parsedDetail,
                MaxResponseChars("community_posts_search"), itemShapeName: "community_posts");
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);
}