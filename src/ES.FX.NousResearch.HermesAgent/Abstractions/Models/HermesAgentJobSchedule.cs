using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The parsed schedule of a stored job. Jobs are created/updated with a schedule STRING
///     (<see cref="HermesAgentJobWrite.Schedule" />); the server parses it and stores/returns this structured
///     object. Exactly one of <see cref="RunAt" /> (kind <c>once</c>), <see cref="Minutes" /> (kind
///     <c>interval</c>) or <see cref="Expr" /> (kind <c>cron</c>) is populated, per <see cref="Kind" />.
/// </summary>
public sealed record HermesAgentJobSchedule
{
    /// <summary>The schedule kind — see <see cref="HermesAgentScheduleKinds" />.</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    /// <summary>
    ///     The absolute run timestamp for one-shot (<c>once</c>) schedules, as an ISO-8601 string. Kept as a
    ///     string (not <see cref="DateTimeOffset" />) because a job created from a naive absolute timestamp
    ///     (e.g. <c>2026-02-03T14:00</c>) may be stored without a UTC offset — the server anchors it to its
    ///     configured Hermes timezone.
    /// </summary>
    [JsonPropertyName("run_at")]
    public string? RunAt { get; init; }

    /// <summary>The recurrence period in minutes for <c>interval</c> schedules.</summary>
    [JsonPropertyName("minutes")]
    public int? Minutes { get; init; }

    /// <summary>The raw cron expression for <c>cron</c> schedules (5-field numeric syntax; names like <c>MON</c> are rejected).</summary>
    [JsonPropertyName("expr")]
    public string? Expr { get; init; }

    /// <summary>The human-readable schedule description (e.g. <c>every 30m</c>); <c>?</c> when nothing is derivable.</summary>
    [JsonPropertyName("display")]
    public string? Display { get; init; }
}
