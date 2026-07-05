using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>A custom field value on a ticket write (<c>{ "id": ..., "value": ... }</c>).</summary>
public sealed record ZendeskCustomFieldWrite
{
    [JsonPropertyName("id")] public long Id { get; init; }

    /// <summary>The value — string, number, bool, or array of strings, matching the field type.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }
}