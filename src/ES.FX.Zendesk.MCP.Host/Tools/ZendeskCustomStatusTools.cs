using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk custom ticket statuses. Namespaced <c>zendesk_custom_statuses_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskCustomStatusTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk custom ticket statuses.</summary>
    [McpServerTool(Name = "zendesk_custom_statuses_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists custom ticket statuses — decodes the custom_status_id carried on tickets when custom statuses are " +
        "enabled. Not paginated: the full list is returned. Filter by active, default, or a comma-separated list " +
        "of status categories (new, open, pending, hold, solved). Read-only.")]
    public Task<ZendeskCustomStatusesResult> List(
        [Description("When set, filters to active (true) or inactive (false) statuses (optional).")]
        bool? active = null,
        [Description("When set, filters to default (true) or non-default (false) statuses (optional).")]
        bool? @default = null,
        [Description("Comma-separated status categories to filter by: new, open, pending, hold, solved (optional).")]
        string? statusCategories = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.CustomStatuses.ListAsync(active: active, @default: @default,
                statusCategories: statusCategories, cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk custom ticket status by id.</summary>
    [McpServerTool(Name = "zendesk_custom_statuses_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single custom ticket status by id — the labels and status category behind a ticket's " +
        "custom_status_id. Read-only.")]
    public Task<ZendeskCustomStatus> Read(
        [Description("The numeric Zendesk custom status id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.CustomStatuses.GetByIdAsync(id, cancellationToken));
}
