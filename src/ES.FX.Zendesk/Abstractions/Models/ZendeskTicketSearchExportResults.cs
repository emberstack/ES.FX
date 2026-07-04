using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of the cursor-based search export (<c>GET /api/v2/search/export.json</c> with
///     <c>filter[type]=ticket</c>). Unlike <c>/search</c>, the export has no 1,000-result cap. Continue paging
///     with <c>Meta.AfterCursor</c> while <c>Meta.HasMore</c> is <c>true</c>; cursors expire after one hour.
/// </summary>
public sealed record ZendeskTicketSearchExportResults
{
    [JsonPropertyName("results")] public IReadOnlyList<ZendeskTicket> Results { get; init; } = [];
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}

/// <summary>The <c>{ "count": n }</c> envelope from <c>GET /api/v2/search/count.json</c> (a plain integer).</summary>
public sealed record ZendeskSearchCountResult
{
    [JsonPropertyName("count")] public long Count { get; init; }
}