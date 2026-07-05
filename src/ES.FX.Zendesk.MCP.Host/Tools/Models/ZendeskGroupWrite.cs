using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a group (create / update). Unset (<c>null</c>) properties are omitted.</summary>
public sealed record ZendeskGroupWrite
{
    /// <summary>The group name. Required on create.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>
    ///     Set <c>false</c> to create a PRIVATE group — a private group can never be made public later, so decide
    ///     at creation time.
    /// </summary>
    [JsonPropertyName("is_public")]
    public bool? IsPublic { get; init; }
}