using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>
///     A suspended ticket — an inbound message held out of the ticket stream (spam suspicion, unverified sender,
///     etc.). Note: its <see cref="Id" /> is a suspended-ticket id, NOT a ticket id.
/// </summary>
public sealed record ZendeskSuspendedTicket
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("subject")] public string? Subject { get; init; }

    /// <summary>Why the message was suspended (e.g. <c>Detected as spam</c>).</summary>
    [JsonPropertyName("cause")]
    public string? Cause { get; init; }

    [JsonPropertyName("author")] public ZendeskSuspendedTicketAuthor? Author { get; init; }
    [JsonPropertyName("recipient")] public string? Recipient { get; init; }
    [JsonPropertyName("brand_id")] public long? BrandId { get; init; }

    /// <summary>The related ticket, when the suspended message was a reply to an existing ticket.</summary>
    [JsonPropertyName("ticket_id")]
    public long? TicketId { get; init; }

    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>The author of a suspended message.</summary>
public sealed record ZendeskSuspendedTicketAuthor
{
    [JsonPropertyName("id")] public long? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}

/// <summary>The <c>{ "suspended_ticket": {...} }</c> envelope.</summary>
public sealed record ZendeskSuspendedTicketResponse
{
    [JsonPropertyName("suspended_ticket")] public ZendeskSuspendedTicket? SuspendedTicket { get; init; }
}

/// <summary>A page of suspended tickets (<c>{ "suspended_tickets": [...] }</c> envelope; cursor-paginated).</summary>
public sealed record ZendeskSuspendedTicketsResult
{
    [JsonPropertyName("suspended_tickets")]
    public IReadOnlyList<ZendeskSuspendedTicket> SuspendedTickets { get; init; } = [];

    [JsonPropertyName("meta")] public ZendeskCursorMeta? Meta { get; init; }
}

/// <summary>
///     The result of recovering suspended tickets. Zendesk's docs and spec disagree on the envelope name
///     (<c>ticket</c> as an array vs <c>tickets</c>), so both are mapped; read <see cref="Recovered" />.
/// </summary>
public sealed record ZendeskSuspendedTicketRecoveryResult
{
    [JsonPropertyName("ticket")] public IReadOnlyList<ZendeskTicket>? TicketArray { get; init; }
    [JsonPropertyName("tickets")] public IReadOnlyList<ZendeskTicket>? Tickets { get; init; }

    /// <summary>The recovered tickets, regardless of which envelope Zendesk used.</summary>
    [JsonIgnore]
    public IReadOnlyList<ZendeskTicket> Recovered => Tickets ?? TicketArray ?? [];
}