using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Execution;
using ES.FX.Zendesk.Support;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk suspended tickets (inbound messages held out of the ticket stream).
///     Namespaced <c>suspended_tickets_*</c>. Ids are suspended-ticket ids, NOT ticket ids.
/// </summary>
/// <remarks>
///     Recovery responses are read as the raw wire JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />)
///     rather than round-tripped through the generated models — the published spec marks the recovered tickets'
///     server-assigned fields (<c>id</c>, <c>created_at</c>/<c>updated_at</c>, ...) as read-only, so Kiota's
///     serializer would silently drop them — and are then projected to lean confirmations: ticket summary rows
///     under the single predictable <c>tickets</c> name (Zendesk's docs and spec disagree on <c>ticket</c> vs
///     <c>tickets</c> for single recovery, so the envelope name is normalized here). Deletes return a
///     <see cref="ZendeskWriteAcknowledgement" /> carrying the structured id/ids.
/// </remarks>
[McpServerToolType]
public sealed class ZendeskSuspendedTicketWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Recovers a suspended ticket into a real ticket.</summary>
    [McpServerTool(Name = "suspended_tickets_recover", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Recover a suspended ticket into a real ticket by suspended-ticket id (NOT a ticket id). Synchronous " +
        "(not an async job); failed recovery returns 422. SIDE EFFECT: sets the recovered ticket's requester to " +
        "the calling agent (not the original) to prevent re-suspension — use suspended_tickets_recover_many with " +
        "one id to preserve the original requester. Returns recovered ticket as a lean summary row under 'tickets'; " +
        "tickets_get for the full record. Write op: rejected in read-only mode, simulated (no changes) in dry-run mode.")]
    public Task<object> Recover(
        [Description(
            "Suspended-ticket's own auto-generated id (from suspended_tickets_list; not a ticket id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"recover suspended ticket {id}",
            async () =>
            {
                var json = await requestAdapter.SendForJsonAsync(
                        zendesk.Api.V2.Suspended_tickets[id].Recover.ToPutRequestInformation(), cancellationToken)
                    .ConfigureAwait(false);
                return SummarizeRecovery(json);
            },
            new { id });

    /// <summary>Recovers up to 100 suspended tickets, preserving their original requesters.</summary>
    [McpServerTool(Name = "suspended_tickets_recover_many", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Recover up to 100 suspended tickets by suspended-ticket ids (synchronous — not an async job). Unlike " +
        "suspended_tickets_recover, PRESERVES the original requesters — prefer it even for a single id when the " +
        "requester matters. Returns recovered tickets as lean summary rows under 'tickets'; recoveries that failed " +
        "and remain suspended come back as suspended-ticket summary rows under 'suspended_tickets'. tickets_get" +
        "(_many) for full records. Write op: rejected in read-only mode, simulated (no changes) in dry-run mode.")]
    public Task<object> RecoverMany(
        [Description(
            "Suspended-ticket auto-generated ids to recover (min 1, max 100; not ticket ids). Ones that fail to " +
            "recover are still included in the response.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"recover {ids.Length} suspended tickets",
            async () =>
            {
                ValidateBulkCount(ids);
                var request = zendesk.Api.V2.Suspended_tickets.Recover_many.ToPutRequestInformation(cfg =>
                    cfg.QueryParameters.Ids = string.Join(',', ids));
                var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
                return SummarizeRecovery(json);
            },
            () =>
            {
                // The dry run enforces the same contract the real call would.
                ValidateBulkCount(ids);
                return ZendeskDryRunResult.ForBulk($"recover {ids.Length} suspended tickets", "recover",
                    "suspended_tickets", ids.Cast<object?>());
            });

    /// <summary>Deletes a suspended ticket by id.</summary>
    [McpServerTool(Name = "suspended_tickets_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Delete a suspended ticket by suspended-ticket id (NOT a ticket id), permanently discarding the held " +
        "message. Returns an acknowledgement carrying the deleted id. Write op: rejected in read-only mode, " +
        "simulated (no changes) in dry-run mode.")]
    public Task<object> Delete(
        [Description(
            "Suspended-ticket's own auto-generated id (from suspended_tickets_list; not a ticket id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete suspended ticket {id}",
            async () =>
            {
                await zendesk.Api.V2.Suspended_tickets[id].DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete suspended ticket {id}.",
                    Id = id
                };
            },
            new { id });

    /// <summary>Deletes up to 100 suspended tickets.</summary>
    [McpServerTool(Name = "suspended_tickets_delete_many", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Delete up to 100 suspended tickets by suspended-ticket ids (synchronous — completes immediately, not an " +
        "async job), permanently discarding the held messages. Returns an acknowledgement carrying the deleted " +
        "ids. Write op: rejected in read-only mode, simulated (no changes) in dry-run mode.")]
    public Task<object> DeleteMany(
        [Description(
            "Suspended-ticket auto-generated ids to delete (min 1, max 100; not ticket ids).")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete {ids.Length} suspended tickets",
            async () =>
            {
                ValidateBulkCount(ids);
                // QUIRK: plain 204 — this bulk delete is synchronous (an acknowledgement, NOT a job status),
                // unlike tickets/destroy_many.
                await zendesk.Api.V2.Suspended_tickets.Destroy_many.DeleteAsync(cfg =>
                    cfg.QueryParameters.Ids = string.Join(',', ids), cancellationToken).ConfigureAwait(false);
                return new ZendeskWriteAcknowledgement
                {
                    Description = $"Zendesk accepted the request to delete {ids.Length} suspended tickets.",
                    Ids = ids
                };
            },
            () =>
            {
                // The dry run enforces the same contract the real call would.
                ValidateBulkCount(ids);
                return ZendeskDryRunResult.ForBulk($"delete {ids.Length} suspended tickets", "delete",
                    "suspended_tickets", ids.Cast<object?>());
            });

    /// <summary>Validates a bulk-operation item count (Zendesk accepts 1–100 items per bulk request).</summary>
    private static void ValidateBulkCount(long[] ids)
    {
        if (ids.Length is 0 or > 100)
            throw new ArgumentException("Zendesk bulk operations accept between 1 and 100 items.", nameof(ids));
    }

    /// <summary>
    ///     Projects a recovery response to lean confirmation rows: the recovered tickets — whichever of the
    ///     <c>ticket</c>/<c>tickets</c> envelope names Zendesk used (its docs and spec disagree for single
    ///     recovery) — become ticket summary rows under the single predictable <c>tickets</c> name, and any
    ///     <c>suspended_tickets</c> rows (recoveries that failed and remain suspended) become suspended-ticket
    ///     summary rows, raw email content stripped.
    /// </summary>
    private static JsonElement SummarizeRecovery(JsonElement response)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            JsonNode.Parse(response.GetRawText()) is not JsonObject source)
            throw new McpException("The Zendesk API returned an empty response where a payload was expected.");

        var result = new JsonObject
        {
            ["tickets"] = SummarizeRows(source["tickets"] ?? source["ticket"], "tickets")
        };
        if (source["suspended_tickets"] is JsonArray stillSuspended)
            result["suspended_tickets"] = SummarizeRows(stillSuspended, "suspended_tickets");
        return JsonSerializer.SerializeToElement(result);
    }

    /// <summary>
    ///     Projects one recovery array through its <see cref="ZendeskLean" /> summary shape. A bare object (the
    ///     third shape the docs/spec disagreement allows for single recovery) is treated as a one-row array.
    /// </summary>
    private static JsonArray SummarizeRows(JsonNode? node, string shapeName)
    {
        var rows = new JsonArray();
        var items = node switch
        {
            JsonArray array => array.OfType<JsonObject>(),
            JsonObject single => [single],
            _ => []
        };
        foreach (var entity in items) rows.Add(ZendeskLean.SummarizeEntity(shapeName, entity));
        return rows;
    }
}