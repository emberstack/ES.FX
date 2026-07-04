using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A page of the cursor-based incremental ticket export
///     (<c>GET /api/v2/incremental/tickets/cursor.json</c>). Continue paging by passing
///     <see cref="AfterCursor" /> as the next request's <c>cursor</c> until <see cref="EndOfStream" /> is
///     <c>true</c>. Subject to Zendesk's incremental-export rate limits.
/// </summary>
public sealed record ZendeskIncrementalTicketsResult
{
    [JsonPropertyName("tickets")] public IReadOnlyList<ZendeskTicket> Tickets { get; init; } = [];

    /// <summary>The cursor to pass as the next request's <c>cursor</c> parameter.</summary>
    [JsonPropertyName("after_cursor")]
    public string? AfterCursor { get; init; }

    /// <summary>Whether the export has reached the end of the stream.</summary>
    [JsonPropertyName("end_of_stream")]
    public bool? EndOfStream { get; init; }

    /// <summary>Sideloaded users (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskUser>? Users { get; init; }

    /// <summary>Sideloaded groups (populated only when the request asks to include <c>groups</c>).</summary>
    [JsonPropertyName("groups")]
    public IReadOnlyList<ZendeskGroup>? Groups { get; init; }

    /// <summary>Sideloaded organizations (populated only when the request asks to include <c>organizations</c>).</summary>
    [JsonPropertyName("organizations")]
    public IReadOnlyList<ZendeskOrganization>? Organizations { get; init; }
}