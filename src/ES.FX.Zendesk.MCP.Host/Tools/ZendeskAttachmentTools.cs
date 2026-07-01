using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk attachments. Namespaced <c>zendesk_attachments_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskAttachmentTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Downloads an attachment's content.</summary>
    [McpServerTool(Name = "zendesk_attachments_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Downloads a ticket attachment's content by id (attachments appear on ticket comments). Text/JSON/CSV/XML " +
        "come back as decoded text ('encoding':'utf-8'); other binary comes back size-capped as base64 " +
        "('encoding':'base64'). 'truncated':true means the file exceeded the size cap and was cut short. Use to read " +
        "a customer's error log, CSV export, or config a screenshot won't convey. Read-only.")]
    public Task<ZendeskAttachmentContent> Read(
        [Description("The numeric attachment id (from a comment's attachments[].id).")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Attachments.GetContentAsync(id, cancellationToken));
}