using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     The result a write tool returns when the effective execution mode is
///     <see cref="Execution.McpExecutionMode.DryRun" />: the request was accepted and validated, but no change
///     was made. The payload states this explicitly so the calling agent is never led to believe the write
///     happened. Single-entity writes echo the request verbatim (small, and the echo IS the verification value);
///     bulk (<c>*_many</c>) writes use the <see cref="ForBulk" /> digest instead — up to 100 full write models
///     echoed back would dwarf any real response the tool could produce.
/// </summary>
public sealed record ZendeskDryRunResult
{
    /// <summary>The digest truncation length for long identity values (subject, external_id).</summary>
    private const int MaxDigestValueChars = 100;

    /// <summary>Always <c>dry_run</c>.</summary>
    [JsonPropertyName("status")]
    public string Status => "dry_run";

    /// <summary>Always <c>false</c> — no change was made.</summary>
    [JsonPropertyName("executed")]
    public bool Executed => false;

    /// <summary>A human-readable statement of the change that would have been made.</summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    ///     The request payload that would have been sent to Zendesk — echoed verbatim for single-entity writes,
    ///     or the <see cref="ForBulk" /> digest for bulk writes.
    /// </summary>
    [JsonPropertyName("request")]
    public object? Request { get; init; }

    /// <summary>
    ///     Creates the dry-run result for a bulk (<c>*_many</c>) write: instead of echoing every item verbatim,
    ///     the request collapses to a digest —
    ///     <c>{ action, target, count, items: [{ index, id/external_id/subject, fields }] }</c> — that still lets
    ///     the agent verify per item <em>which</em> record is addressed and <em>which</em> fields would change,
    ///     with long identity values truncated. Primitive items (id lists, e.g. bulk deletes) digest to
    ///     <c>{ index, id }</c>.
    /// </summary>
    /// <param name="action">
    ///     The action in the infinitive for the human-readable description, matching the tool's
    ///     <c>ZendeskToolInvoker</c> action (for example <c>"update 3 tickets"</c>).
    /// </param>
    /// <param name="operation">The digest's <c>action</c> verb (for example <c>"update"</c>).</param>
    /// <param name="target">The digest's <c>target</c> — the plural entity name (for example <c>"tickets"</c>).</param>
    /// <param name="items">The write models (or ids) the bulk call would send, in request order.</param>
    /// <param name="serializerOptions">
    ///     The serializer options the tool uses for its request payload, so the digest reports the same wire
    ///     field names and the same present/absent field set. Defaults to the tool models' annotated names with
    ///     nulls treated as absent.
    /// </param>
    public static ZendeskDryRunResult ForBulk(string action, string operation, string target,
        IEnumerable<object?> items, JsonSerializerOptions? serializerOptions = null)
    {
        var digestItems = new JsonArray();
        var index = 0;
        foreach (var item in items)
        {
            digestItems.Add(DigestItem(index, JsonSerializer.SerializeToNode(item, serializerOptions)));
            index++;
        }

        return new ZendeskDryRunResult
        {
            Description = $"Dry run — no changes were made. This call would {action}.",
            Request = new JsonObject
            {
                ["action"] = operation,
                ["target"] = target,
                ["count"] = index,
                ["items"] = digestItems
            }
        };
    }

    /// <summary>
    ///     Digests one bulk item: its position, the identity fields an agent can recognize it by (id verbatim;
    ///     external_id/subject truncated), and the names of the fields the write would send. Null-valued
    ///     properties count as absent — they are omitted from the request wire format too.
    /// </summary>
    private static JsonObject DigestItem(int index, JsonNode? item)
    {
        var row = new JsonObject { ["index"] = index };
        switch (item)
        {
            case JsonObject entity:
            {
                if (entity["id"] is { } id) row["id"] = id.DeepClone();
                CopyIdentityValue(entity, row, "external_id");
                CopyIdentityValue(entity, row, "subject");
                var fields = new JsonArray();
                foreach (var (name, value) in entity)
                    if (value is not null && name != "id")
                        fields.Add(name);
                row["fields"] = fields;
                break;
            }
            case JsonValue value:
                // A primitive item is an id-style value (bulk deletes and friends send plain id lists).
                row["id"] = value.DeepClone();
                break;
        }

        return row;
    }

    private static void CopyIdentityValue(JsonObject entity, JsonObject row, string field)
    {
        if (entity[field] is not { } value) return;
        row[field] = value is JsonValue jsonValue && jsonValue.TryGetValue(out string? text)
            ? ZendeskLean.Truncate(text, MaxDigestValueChars)
            : value.DeepClone();
    }
}