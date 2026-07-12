using System.Runtime.CompilerServices;
using ES.FX.Zendesk.MCP.Host.Tests.Testing;
using ES.FX.Zendesk.MCP.Host.Tools;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     OAS-coupled staleness tests, part 2 (design A6.2): for every summarized entity, every property of its
///     vendored OAS schema must be explicitly classified <c>summary</c> (on the allowlist) or <c>omitted</c>
///     (deliberately stripped) in a committed snapshot under <c>OasClassification/</c>. When a re-vendored spec
///     brings new Zendesk fields, they arrive unclassified, the build fails, and someone must triage them into a
///     shape or an explicit omission — allowlists can no longer drift silently.
/// </summary>
/// <remarks>
///     REGENERATION (same pattern as <see cref="ZendeskToolProfileSnapshotTests" />): set
///     <c>REGENERATE_OAS_CLASSIFICATION=1</c> and run these tests once — each rewrites its committed snapshot
///     (in the source tree, via <see cref="SourceSnapshotsDirectory" />) from the current OAS + allowlists and
///     fails with a "regenerated" message. Review the diff (every classification flip is a behavior decision),
///     commit, and re-run without the variable.
/// </remarks>
public class ZendeskLeanOasClassificationSnapshotTests
{
    private const string RegenerateEnvVar = "REGENERATE_OAS_CLASSIFICATION";

    /// <summary>
    ///     The summarized entities that have a schema in the vendored specs. <c>side_conversations</c> is absent
    ///     by design — its endpoint does not exist in the published spec (see the spec-anomaly ledger); the
    ///     entity-level exemption is pinned by <see cref="ZendeskLeanOasExistenceTests" />.
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
            ["categories"] = (ZendeskOasDocument.HelpCenter, "CategoryObject"),
            ["satisfaction_ratings"] = (ZendeskOasDocument.Support, "SatisfactionRatingObject"),
            ["community_posts"] = (ZendeskOasDocument.HelpCenter, "PostObject"),
            ["custom_objects"] = (ZendeskOasDocument.Support, "CustomObject"),
            ["custom_object_records"] = (ZendeskOasDocument.Support, "CustomObjectRecord")
        };

    public static TheoryData<string> ClassifiedEntities()
    {
        var data = new TheoryData<string>();
        foreach (var entity in EntitySchemas.Keys.Order(StringComparer.Ordinal)) data.Add(entity);
        return data;
    }

    [Theory]
    [MemberData(nameof(ClassifiedEntities))]
    public void Every_Schema_Property_Is_Classified_In_The_Committed_Snapshot(string entity)
    {
        var (document, schema) = EntitySchemas[entity];
        var allowlist = ZendeskLean.SummarySourceFields[entity].ToHashSet(StringComparer.Ordinal);

        // The CURRENT classification: every OAS property, marked summary when the allowlist reads it.
        var current = ZendeskOasSchemas.PropertyNames(document, schema)
            .Order(StringComparer.Ordinal)
            .ToDictionary(field => field, field => allowlist.Contains(field) ? "summary" : "omitted",
                StringComparer.Ordinal);
        Assert.NotEmpty(current);
        var content = string.Join('\n', current.Select(pair => $"{pair.Key} {pair.Value}")) + "\n";

        var fileName = $"{entity}.classification.txt";
        if (Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1")
        {
            var directory = SourceSnapshotsDirectory();
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, fileName);
            File.WriteAllText(sourcePath, content);
            Assert.Fail($"Regenerated {fileName} ({current.Count} properties) at {sourcePath}. Review the " +
                        $"classification diff, commit the file and re-run without {RegenerateEnvVar}=1.");
        }

        // Read the committed bytes (copied next to the test assembly); normalize CRLF so autocrlf checkouts
        // cannot fail on line endings alone.
        var committedPath = Path.Combine(AppContext.BaseDirectory, "OasClassification", fileName);
        Assert.True(File.Exists(committedPath),
            $"Missing committed classification snapshot '{fileName}'. Set {RegenerateEnvVar}=1 and re-run to " +
            "generate it.");
        var committed = File.ReadAllText(committedPath).Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : "", StringComparer.Ordinal);

        // Three focused assertions so drift reports the exact fields instead of a byte-blob diff.
        var unclassified = current.Keys.Except(committed.Keys).ToList();
        Assert.True(unclassified.Count == 0,
            $"New OAS properties on '{entity}' are not classified yet: {string.Join(", ", unclassified)}. " +
            "Triage each one — add it to the summary shape (and SummarySourceFields) or accept the omission — " +
            $"then regenerate the snapshot with {RegenerateEnvVar}=1.");

        var stale = committed.Keys.Except(current.Keys).ToList();
        Assert.True(stale.Count == 0,
            $"Classified fields no longer exist on '{entity}' in the OAS: {string.Join(", ", stale)}. Zendesk " +
            $"removed/renamed them — review the shape, then regenerate with {RegenerateEnvVar}=1.");

        var flipped = current.Where(pair => committed[pair.Key] != pair.Value)
            .Select(pair => $"{pair.Key} (snapshot: {committed[pair.Key]}, code: {pair.Value})").ToList();
        Assert.True(flipped.Count == 0,
            $"Classification changed for '{entity}': {string.Join(", ", flipped)}. If intentional, regenerate " +
            $"with {RegenerateEnvVar}=1 and commit the reviewed diff.");
    }

    /// <summary>
    ///     The <c>OasClassification/</c> directory in the source tree, resolved from this file's compile-time
    ///     path so regeneration writes the committed files rather than the build-output copies.
    /// </summary>
    private static string SourceSnapshotsDirectory([CallerFilePath] string thisFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(thisFilePath)!, "..", "OasClassification");
}