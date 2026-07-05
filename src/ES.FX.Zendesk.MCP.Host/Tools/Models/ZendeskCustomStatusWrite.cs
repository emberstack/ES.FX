using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a custom ticket status (create / update).</summary>
public sealed record ZendeskCustomStatusWrite
{
    /// <summary>
    ///     The built-in category — see <see cref="ZendeskStatusCategories" />. Allowed values: new, open, pending,
    ///     hold, solved. Required on create; immutable afterwards (not accepted on update).
    /// </summary>
    [JsonPropertyName("status_category")]
    public string? StatusCategory { get; init; }

    /// <summary>The label agents see (max 48 chars). Required on create.</summary>
    [JsonPropertyName("agent_label")]
    public string? AgentLabel { get; init; }

    /// <summary>The label end users see (max 48 chars).</summary>
    [Description("The label end users see (optional, max 48 characters).")]
    [JsonPropertyName("end_user_label")]
    public string? EndUserLabel { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("end_user_description")]
    public string? EndUserDescription { get; init; }

    [JsonPropertyName("active")] public bool? Active { get; init; }
}