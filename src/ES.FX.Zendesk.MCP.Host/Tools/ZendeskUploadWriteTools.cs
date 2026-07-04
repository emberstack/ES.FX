using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP write tools for Zendesk uploads (attachment uploads consumed by ticket comments).
///     Namespaced <c>zendesk_uploads_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskUploadWriteTools(IZendeskClient zendeskApiClient, IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Uploads a file for attaching to a ticket comment.</summary>
    [McpServerTool(Name = "zendesk_uploads_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = true)]
    [Description(
        "Uploads a file to Zendesk (50 MB limit) for attaching to a ticket comment. Returns an upload whose 'token' " +
        "is passed to the ticket comment's uploads when creating/updating a ticket; to bundle multiple files onto " +
        "one token, pass the returned token back into this tool for each subsequent file. Tokens are single-use and " +
        "expire after 60 minutes; until consumed, the file is reachable by any authenticated user via its " +
        "content_url. Discard an unwanted upload with zendesk_uploads_delete. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Create(
        [Description("The file name; its extension must match the actual content.")]
        string fileName,
        [Description("The file content as base64-encoded bytes.")]
        string contentBase64,
        [Description("The file's real MIME type (e.g. 'image/png'); a wrong type causes undesired behavior.")]
        string contentType,
        [Description("An existing upload token to append this file to, for multi-file bundles (optional).")]
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            // Precise, actionable message; the SDK would otherwise mask FormatException to a generic error.
            throw new McpException("The 'contentBase64' parameter is not valid base64-encoded content.");
        }

        return ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"upload file '{fileName}' ({contentType}, {content.Length} bytes)",
            () => zendeskApiClient.Uploads.UploadAsync(fileName, content, contentType, token: token,
                cancellationToken: cancellationToken),
            new { fileName, contentType, contentLength = content.Length, token });
    }

    /// <summary>Deletes an unconsumed upload by its token.</summary>
    [McpServerTool(Name = "zendesk_uploads_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Deletes an unconsumed Zendesk upload by its token, discarding the uploaded file(s) before they are " +
        "attached to a comment. Only works while the token is still valid (unconsumed, under 60 minutes old). " +
        "Returns a completion acknowledgement. " +
        "Write operation — honors the server execution mode: rejected in read-only mode, simulated (no changes made) in dry-run mode.")]
    public Task<object> Delete(
        [Description("The upload token returned by zendesk_uploads_create.")]
        string token,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete upload token '{token}'",
            () => zendeskApiClient.Uploads.DeleteAsync(token, cancellationToken: cancellationToken), new { token });
}
