using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk macros (canned responses). Namespaced <c>zendesk_macros_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskMacroTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists Zendesk macros.</summary>
    [McpServerTool(Name = "zendesk_macros_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Zendesk macros — the library of canned responses and bulk actions agents apply to resolve common " +
        "issues (refunds, password resets, escalations). Match a customer's problem to a macro, then call " +
        "zendesk_macros_read to see its exact reply text and side effects. Read-only.")]
    public Task<ZendeskMacrosResult> List(
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 100).")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Macros.ListAsync(page, perPage, cancellationToken));

    /// <summary>Returns a single macro including its actions.</summary>
    [McpServerTool(Name = "zendesk_macros_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single macro including its actions — the canned reply body plus any field/tag/status changes it " +
        "would apply. Read-only.")]
    public Task<ZendeskMacro> Read(
        [Description("The numeric macro id.")] long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Macros.GetByIdAsync(id, cancellationToken));
}