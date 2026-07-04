using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Cursor-pagination metadata (<c>meta</c>) returned by endpoints that support Zendesk cursor pagination
///     (<c>page[size]</c> / <c>page[after]</c>). Pass <see cref="AfterCursor" /> as the next request's
///     <c>afterCursor</c> while <see cref="HasMore" /> is <c>true</c>.
/// </summary>
public sealed record ZendeskCursorMeta
{
    /// <summary>Whether more records exist after this page.</summary>
    [JsonPropertyName("has_more")]
    public bool? HasMore { get; init; }

    /// <summary>The cursor for the page after this one.</summary>
    [JsonPropertyName("after_cursor")]
    public string? AfterCursor { get; init; }

    /// <summary>The cursor for the page before this one.</summary>
    [JsonPropertyName("before_cursor")]
    public string? BeforeCursor { get; init; }
}