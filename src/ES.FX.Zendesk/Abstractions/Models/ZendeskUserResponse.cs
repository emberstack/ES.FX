using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     Envelope for a single-user Zendesk response (<c>{ "user": { ... } }</c>).
/// </summary>
public sealed record ZendeskUserResponse
{
    /// <summary>The user payload.</summary>
    [JsonPropertyName("user")]
    public ZendeskUser? User { get; init; }
}