using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     OAS-coupled staleness tests, part 1 (design A6.1): every field a <see cref="ZendeskLean" /> summary
///     allowlist reads must exist as a property of its entity's schema in the vendored OpenAPI specs. Allowlists
///     are destructive by default — a field Zendesk renames or removes would silently vanish from summary rows —
///     so this test makes the drift fail loudly at re-vendor time instead. Also pins the declarative
///     <see cref="ZendeskLean.SummarySourceFields" /> map to the actual shape implementations, in both
///     directions, so the map the OAS tests rely on cannot rot.
/// </summary>
public class ZendeskLeanOasExistenceTests
{
    /// <summary>
    ///     Maps each summary entity (Zendesk envelope array name) to the OAS schema its rows are instances of.
    ///     <c>side_conversations</c> is deliberately absent: the side-conversations endpoint does not exist in
    ///     the published spec at all (recorded in the spec-anomaly ledger, src/ES.FX.Zendesk/OpenApi/README.md),
    ///     so there is no schema to check against — <see cref="Every_Summary_Entity_Is_Schema_Mapped_Or_Exempt" />
    ///     keeps that exemption explicit.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (ZendeskOasDocument Document, string Schema)>
        EntitySchemas = new Dictionary<string, (ZendeskOasDocument, string)>(StringComparer.Ordinal)
        {
            ["tickets"] = (ZendeskOasDocument.Support, "TicketObject"),
            ["users"] = (ZendeskOasDocument.Support, "UserObject"),
            ["organizations"] = (ZendeskOasDocument.Support, "OrganizationObject"),
            ["articles"] = (ZendeskOasDocument.HelpCenter, "ArticleObject"),
            ["groups"] = (ZendeskOasDocument.Support, "GroupObject"),
            ["macros"] = (ZendeskOasDocument.Support, "MacroObject"),
            ["views"] = (ZendeskOasDocument.Support, "ViewObject"),
            ["ticket_fields"] = (ZendeskOasDocument.Support, "TicketFieldObject"),
            ["ticket_forms"] = (ZendeskOasDocument.Support, "TicketFormObject"),
            ["brands"] = (ZendeskOasDocument.Support, "BrandObject"),
            ["custom_statuses"] = (ZendeskOasDocument.Support, "CustomStatusObject"),
            ["suspended_tickets"] = (ZendeskOasDocument.Support, "SuspendedTicketObject"),
            ["identities"] = (ZendeskOasDocument.Support, "UserIdentityObject"),
            ["attachments"] = (ZendeskOasDocument.Support, "AttachmentObject"),
            ["job_statuses"] = (ZendeskOasDocument.Support, "JobStatusObject"),
            ["audits"] = (ZendeskOasDocument.Support, "TicketAuditObject"),
            ["sections"] = (ZendeskOasDocument.HelpCenter, "SectionObject"),
            ["categories"] = (ZendeskOasDocument.HelpCenter, "CategoryObject")
        };

    /// <summary>
    ///     Allowlist fields that intentionally have NO property in the entity's schema. Each entry must stay
    ///     truly absent from the schema — a stale exemption (the field appeared in the spec) fails the test too.
    ///     <list type="bullet">
    ///         <item>
    ///             <c>articles.snippet</c> — materialized by Help Center search only; it lives on
    ///             <c>ArticleSearchResponse</c>, not on <c>ArticleObject</c> (the summary shape passes it
    ///             through when present).
    ///         </item>
    ///     </list>
    /// </summary>
    private static readonly IReadOnlySet<(string Entity, string Field)> SchemalessFields =
        new HashSet<(string, string)> { ("articles", "snippet") };

    /// <summary>
    ///     Fields consumed by a shape to produce a computed output instead of being copied verbatim. Used by the
    ///     shape-coherence test to know which row keys to expect.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string[] Consumed, string[] Produced)> ComputedOutputs =
        new Dictionary<string, (string[], string[])>(StringComparer.Ordinal)
        {
            // Drop-down options collapse to a computed count (ticket_fields_get carries the values).
            ["ticket_fields"] = (["custom_field_options", "system_field_options"], ["options_count"]),
            // The heavy per-item results array collapses to succeeded/failed counts plus the first failures.
            ["job_statuses"] = (["results"], ["results_summary"])
        };

    public static TheoryData<string> SummaryEntities()
    {
        var data = new TheoryData<string>();
        foreach (var entity in ZendeskLean.SummarySourceFields.Keys.Order(StringComparer.Ordinal)) data.Add(entity);
        return data;
    }

    [Fact]
    public void Every_Summary_Entity_Is_Schema_Mapped_Or_Exempt()
    {
        // side_conversations is the single, deliberate exemption (endpoint absent from the published spec).
        var expected = ZendeskLean.SummarySourceFields.Keys.Where(entity => entity != "side_conversations")
            .Order(StringComparer.Ordinal);
        Assert.Equal(expected, EntitySchemas.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(SummaryEntities))]
    public void Every_Allowlist_Field_Exists_In_The_Vendored_Schema(string entity)
    {
        if (!EntitySchemas.TryGetValue(entity, out var schema)) return; // exempt (asserted above)
        var properties = ZendeskOasSchemas.PropertyNames(schema.Document, schema.Schema);
        Assert.NotEmpty(properties);

        var missing = ZendeskLean.SummarySourceFields[entity]
            .Where(field => !SchemalessFields.Contains((entity, field)) && !properties.Contains(field))
            .ToList();
        Assert.True(missing.Count == 0,
            $"Summary allowlist fields for '{entity}' are missing from OAS schema {schema.Schema}: " +
            $"{string.Join(", ", missing)}. Zendesk renamed/removed them, or the allowlist has a typo — " +
            "triage the shape before re-vendoring silently drops the data.");

        // Exemptions must stay real: once the spec gains the property, the exemption is stale.
        var stale = SchemalessFields.Where(exempt => exempt.Entity == entity && properties.Contains(exempt.Field))
            .ToList();
        Assert.True(stale.Count == 0,
            $"Stale schema exemptions for '{entity}': {string.Join(", ", stale.Select(exempt => exempt.Field))} " +
            "now exist in the OAS — remove them from SchemalessFields.");
    }

    [Fact]
    public void The_Allowlist_Map_And_The_Registered_Shapes_Cover_The_Same_Entities() =>
        Assert.Equal(ZendeskLean.SummaryShapeNames.Order(StringComparer.Ordinal),
            ZendeskLean.SummarySourceFields.Keys.Order(StringComparer.Ordinal));

    /// <summary>
    ///     Pins <see cref="ZendeskLean.SummarySourceFields" /> to the shape implementations: an entity carrying
    ///     every allowlisted field (plus a poison field) must produce a row containing exactly the declared
    ///     fields (with the declared computed transforms) and nothing else. A field added to a shape but not the
    ///     map — or vice versa — fails here, which is what lets the OAS tests trust the map.
    /// </summary>
    [Theory]
    [MemberData(nameof(SummaryEntities))]
    public void Each_Summary_Shape_Materializes_Exactly_Its_Declared_Allowlist(string entity)
    {
        var allowlist = ZendeskLean.SummarySourceFields[entity];
        var fullEntity = new JsonObject { ["__not_allowlisted__"] = "poison" };
        foreach (var field in allowlist) fullEntity[field] = SyntheticValue(field);

        var row = ZendeskLean.SummarizeEntity(entity, fullEntity);
        Assert.NotNull(row);

        var (consumed, produced) = ComputedOutputs.TryGetValue(entity, out var computed)
            ? computed
            : (Array.Empty<string>(), Array.Empty<string>());
        var expected = allowlist.Except(consumed).Concat(produced).Order(StringComparer.Ordinal);
        Assert.Equal(expected, row.Select(property => property.Key).Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     A present, non-empty value for a synthesized entity field — structured where the shape reads inside
    ///     the value (via/author/participants/options/results/events), a plain string everywhere else (the
    ///     allowlist copy is type-agnostic).
    /// </summary>
    private static JsonNode SyntheticValue(string field) => field switch
    {
        "via" => new JsonObject
        {
            ["channel"] = "web",
            ["source"] = new JsonObject
            {
                ["rel"] = "trigger",
                ["from"] = new JsonObject { ["id"] = 1, ["title"] = "Rule" }
            }
        },
        "author" => new JsonObject { ["name"] = "Sender", ["email"] = "sender@example.org" },
        "participants" => new JsonArray(new JsonObject { ["user_id"] = 1, ["email"] = "p@example.org" }),
        "custom_field_options" => new JsonArray(new JsonObject { ["id"] = 1, ["name"] = "Low", ["value"] = "low" }),
        "system_field_options" => new JsonArray(new JsonObject { ["name"] = "Open", ["value"] = "open" }),
        "results" => new JsonArray(new JsonObject { ["id"] = 1, ["index"] = 0, ["error"] = "TooManyValues" }),
        "events" => new JsonArray(new JsonObject
        {
            ["id"] = 1, ["type"] = "Change", ["field_name"] = "status",
            ["previous_value"] = "open", ["value"] = "solved"
        }),
        _ => JsonValue.Create("value")
    };
}