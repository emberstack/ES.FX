using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A Zendesk ticket form (see <c>GET /api/v2/ticket_forms/{id}.json</c>).
/// </summary>
public sealed record ZendeskTicketForm
{
    /// <summary>The automatically assigned form identifier.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>The API URL of the form.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>The mandatory internal name of the form.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The name shown to end users.</summary>
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>Whether the form is active.</summary>
    [JsonPropertyName("active")]
    public bool Active { get; init; }

    /// <summary>Whether this is the default form.</summary>
    [JsonPropertyName("default")]
    public bool Default { get; init; }

    /// <summary>Whether the form is visible to end users.</summary>
    [JsonPropertyName("end_user_visible")]
    public bool EndUserVisible { get; init; }

    /// <summary>The relative position of the form.</summary>
    [JsonPropertyName("position")]
    public int? Position { get; init; }

    /// <summary>The ids of the ticket fields on the form.</summary>
    [JsonPropertyName("ticket_field_ids")]
    public IReadOnlyList<long>? TicketFieldIds { get; init; }

    /// <summary>When the form was created.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the form was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }
}