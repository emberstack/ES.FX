using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools.Models;

/// <summary>The writable fields of a custom field option (upsert: include <see cref="Id" /> to update).</summary>
public sealed record ZendeskCustomFieldOptionWrite
{
    /// <summary>Include to UPDATE an existing option; omit to create one.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>The human-readable label for the option. Required together with <see cref="Value" />.</summary>
    [Description("Display label for the option (required together with value).")]
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    ///     The tag stored on tickets that select this option. Required together with <see cref="Name" />. A value
    ///     cannot collide with a tag used by a <c>checkbox</c> field, since tags aren't reusable across custom
    ///     fields.
    /// </summary>
    [Description(
        "The tag stored on tickets that select this option (required together with name; cannot reuse a tag " +
        "already used by another custom field, e.g. a checkbox field's tag).")]
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [Description("Optional int: display order among options.")]
    [JsonPropertyName("position")]
    public long? Position { get; init; }

    /// <summary>
    ///     Whether a ticket can be solved while this option is selected. Include it when replacing a field's
    ///     option set — omitted options are recreated with the Zendesk default otherwise.
    /// </summary>
    [Description(
        "Optional bool: whether a ticket may be solved while this option is selected, when the field is " +
        "required to solve.")]
    [JsonPropertyName("allow_solving")]
    public bool? AllowSolving { get; init; }
}