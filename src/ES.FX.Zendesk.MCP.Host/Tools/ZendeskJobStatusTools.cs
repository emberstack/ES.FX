using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk job statuses — the async jobs returned by bulk operations. Namespaced
///     <c>job_statuses_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskJobStatusTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists recent Zendesk job statuses.</summary>
    [McpServerTool(Name = "job_statuses_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists recent Zendesk job statuses (the async jobs returned by bulk write tools). Zendesk retains job " +
        "status data for roughly one day. Cursor pagination: pass pageSize/afterCursor; the result's " +
        "meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskJobStatusesResult> List(
        [Description("The cursor page size (optional, max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.JobStatuses.ListAsync(pageSize: pageSize, afterCursor: afterCursor,
                cancellationToken: cancellationToken));

    /// <summary>Returns a Zendesk job status by id.</summary>
    [McpServerTool(Name = "job_statuses_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk job status by its string id. Poll this after bulk write tools (which return a " +
        "job_status) until 'status' is 'completed' or 'failed'; 'results' then carries the per-item outcomes. " +
        "Job status data is retained for roughly one day. Read-only.")]
    public Task<ZendeskJobStatus> Read(
        [Description("The job status id (a string, returned by the bulk operation that started the job).")]
        string id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.JobStatuses.GetByIdAsync(id, cancellationToken));

    /// <summary>Returns many Zendesk job statuses in one request.</summary>
    [McpServerTool(Name = "job_statuses_get_many", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns up to 100 Zendesk job statuses in one request (more than 100 ids is rejected by design). Use to " +
        "poll several bulk jobs at once instead of calling job_statuses_get repeatedly. Read-only.")]
    public Task<ZendeskJobStatusesResult> ReadMany(
        [Description("The job status ids (strings, at most 100).")]
        string[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.JobStatuses.GetManyAsync(ids, cancellationToken));
}
