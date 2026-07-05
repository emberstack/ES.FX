using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     The writable fields of a scheduled job, used for both create (<c>POST /api/jobs</c>) and partial update
///     (<c>PATCH /api/jobs/{job_id}</c>). Unset (<c>null</c>) properties are omitted from the request, so an
///     update sends only the fields you set (the server applies a shallow merge; omitted fields are untouched).
///     Note the write/read asymmetry: <see cref="Schedule" /> and <see cref="Repeat" /> are written as a string
///     and an integer, but the stored job returns them as structured objects
///     (<see cref="HermesAgentJobSchedule" /> / <see cref="HermesAgentJobRepeat" />).
/// </summary>
public sealed record HermesAgentJobWrite
{
    /// <summary>The job name (required on create; max 200 characters).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The schedule as a STRING (required on create). Accepted grammars: a relative duration
    ///     (<c>30m</c>, <c>2h</c>, <c>1d</c> — one-shot), <c>every {duration}</c> (recurring interval,
    ///     minute granularity), a 5-field numeric cron expression (<c>*/5 * * * *</c>; names like <c>MON</c>
    ///     are rejected), or an absolute ISO-8601 timestamp (one-shot; naive values are anchored to the
    ///     server's timezone). The server parses it into a <see cref="HermesAgentJobSchedule" />. QUIRK: an
    ///     unparseable schedule string fails with HTTP <c>500</c>, not 400.
    /// </summary>
    [JsonPropertyName("schedule")]
    public string? Schedule { get; init; }

    /// <summary>
    ///     The agent prompt to run (max 5000 characters). Non-empty prompts are threat-scanned server-side —
    ///     injection/exfiltration payloads and invisible Unicode are rejected with <c>400</c>.
    /// </summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; init; }

    /// <summary>
    ///     The delivery target(s) — a single token or comma-separated fan-out list (e.g. <c>telegram</c>,
    ///     <c>origin,all</c>). Defaults to <c>local</c> (see <see cref="HermesAgentDeliverModes" />) when
    ///     omitted on create. Targets are not validated at write time — a bad target surfaces later on
    ///     <see cref="HermesAgentJob.LastDeliveryError" />.
    /// </summary>
    [JsonPropertyName("deliver")]
    public string? Deliver { get; init; }

    /// <summary>
    ///     The skills to inject for the run. On create an empty list is treated the same as absent; on update
    ///     an empty list clears all skills.
    /// </summary>
    [JsonPropertyName("skills")]
    public IReadOnlyList<string>? Skills { get; init; }

    /// <summary>
    ///     The total number of runs before the job auto-deletes (must be a positive integer; the server rejects
    ///     anything else with <c>400</c>). When omitted on create, one-shot schedules default to a single run
    ///     and interval/cron schedules run forever. FOOTGUN (confirmed server behavior): on update the server
    ///     stores this bare integer verbatim, replacing the stored <c>{times, completed}</c> object with a
    ///     corrupt shape — avoid setting it on <c>PATCH</c>; set the repeat budget at create time instead.
    /// </summary>
    [JsonPropertyName("repeat")]
    public int? Repeat { get; init; }
}
