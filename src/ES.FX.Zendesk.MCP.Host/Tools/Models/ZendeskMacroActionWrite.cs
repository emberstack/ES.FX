using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>A macro action on a write (<c>{ "field": ..., "value": ... }</c>; see the actions reference).</summary>
public sealed record ZendeskMacroActionWrite
{
    [JsonPropertyName("field")]
    [Description(
        "The change the macro applies, as one { field, value } item. Common action fields and values: " +
        "status: open|pending|hold|solved (macros reject new/closed); priority: low|normal|high|urgent; " +
        "type: question|incident|problem|task; group_id: a numeric id string or \"\" (unassign); " +
        "assignee_id: a numeric id string, \"current_user\", or \"\"; set_tags (replaces), current_tags (adds), " +
        "remove_tags (removes) — each a space-delimited tag string; comment_value: a plain string OR a " +
        "two-element [channel, text] array where channel is all|web|chat; comment_value_html: HTML comment; " +
        "comment_mode_is_public: boolean; subject: string; custom_fields_<id>: a custom-field value.")]
    public string? Field { get; init; }

    /// <summary>The action value — a string, or an array of strings for multi-value actions.</summary>
    [JsonPropertyName("value")]
    [Description(
        "The action value. Always sent as a string (or the [channel, text] array for comment_value where " +
        "channel is all|web|chat).")]
    public object? Value { get; init; }
}