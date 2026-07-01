using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A macro: a reusable set of <see cref="Actions" /> (canned reply + field/tag/status changes) an agent can
///     apply to resolve common issues.
/// </summary>
public sealed record ZendeskMacro
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("position")] public int? Position { get; init; }

    /// <summary>The actions the macro applies (the reply comment is in the <c>comment_value</c> action).</summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<ZendeskMacroAction>? Actions { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}