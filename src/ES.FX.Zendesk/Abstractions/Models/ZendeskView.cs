using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>A Zendesk view — a saved, shared ticket filter (see <c>GET /api/v2/views.json</c>).</summary>
public sealed record ZendeskView
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
    [JsonPropertyName("default")] public bool? Default { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The <c>{ "view": {...} }</c> envelope.</summary>
public sealed record ZendeskViewResponse
{
    [JsonPropertyName("view")] public ZendeskView? View { get; init; }
}

/// <summary>A page of views (<c>{ "views": [...] }</c> envelope).</summary>
public sealed record ZendeskViewsResult
{
    [JsonPropertyName("views")] public IReadOnlyList<ZendeskView> Views { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }
    [JsonPropertyName("previous_page")] public string? PreviousPage { get; init; }
    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}

/// <summary>
///     A cached ticket count for a view (<c>{ "view_count": {...} }</c> envelope). Counts for large views are
///     approximate — <see cref="Fresh" /> indicates whether the value is current.
/// </summary>
public sealed record ZendeskViewCount
{
    [JsonPropertyName("view_id")] public long? ViewId { get; init; }
    [JsonPropertyName("value")] public long? Value { get; init; }
    [JsonPropertyName("fresh")] public bool? Fresh { get; init; }
}

/// <summary>The <c>{ "view_count": {...} }</c> envelope.</summary>
public sealed record ZendeskViewCountResponse
{
    [JsonPropertyName("view_count")] public ZendeskViewCount? ViewCount { get; init; }
}