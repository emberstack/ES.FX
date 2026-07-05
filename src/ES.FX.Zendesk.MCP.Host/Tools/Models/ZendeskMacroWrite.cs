using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a macro (create / update).</summary>
public sealed record ZendeskMacroWrite
{
    /// <summary>The macro title. Required on create.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    ///     The macro actions. Required on create. Each action is a <c>{ field, value }</c> pair where
    ///     <c>field</c> names a ticket field to modify (e.g. <c>"status"</c>) and <c>value</c> is its new value
    ///     (e.g. <c>"solved"</c>); <c>value</c> is a string, or an array of strings for multi-value actions.
    ///     DESTRUCTIVE on update: the whole array is replaced — include ALL actions when changing any one.
    /// </summary>
    [Description(
        "The changes the macro applies, as { field, value } items. Common action fields and values: " +
        "status: open|pending|hold|solved (macros reject new/closed); priority: low|normal|high|urgent; " +
        "type: question|incident|problem|task; group_id: a numeric id string or \"\" (unassign); " +
        "assignee_id: a numeric id string, \"current_user\", or \"\"; set_tags (replaces), current_tags (adds), " +
        "remove_tags (removes) — each a space-delimited tag string; comment_value: a plain string OR a " +
        "two-element [channel, text] array where channel is all|web|chat; comment_value_html: HTML comment; " +
        "comment_mode_is_public: boolean; subject: string; custom_fields_<id>: a custom-field value. " +
        "Value is always sent as a string (or the [channel, text] array for comment_value).")]
    [JsonPropertyName("actions")]
    public IReadOnlyList<ZendeskMacroActionWrite>? Actions { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("position")] public long? Position { get; init; }
}