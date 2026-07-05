using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk macros (canned responses). Namespaced <c>macros_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskMacroTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk macros.</summary>
    [McpServerTool(Name = "macros_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk macros — the library of canned responses and bulk actions agents apply to resolve common " +
        "issues (refunds, password resets, escalations). Match a customer's problem to a macro, then call " +
        "macros_get to see its exact reply text and side effects. Read-only.")]
    public Task<ZendeskMacrosResult> List(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100 — Zendesk clamps higher values to 100). The total is in 'count'; " +
            "a non-null 'next_page' means more pages.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Macros.ListAsync(page, perPage, cancellationToken));

    /// <summary>Lists only the macros usable by the current agent.</summary>
    [McpServerTool(Name = "macros_list_active", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists only the active macros usable by the current agent — a pre-filtered view of macros_list " +
        "excluding inactive or inaccessible macros. Read-only.")]
    public Task<ZendeskMacrosResult> ListActive(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100 — Zendesk clamps higher values to 100). The total is in 'count'; " +
            "a non-null 'next_page' means more pages.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Macros.ListActiveAsync(page, perPage, cancellationToken));

    /// <summary>Returns a single macro including its actions.</summary>
    [McpServerTool(Name = "macros_get", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single macro including its actions — the canned reply body plus any field/tag/status changes it " +
        "would apply. Read-only.")]
    public Task<ZendeskMacro> Read(
        [Description("The numeric macro id.")] long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Macros.GetByIdAsync(id, cancellationToken));
}