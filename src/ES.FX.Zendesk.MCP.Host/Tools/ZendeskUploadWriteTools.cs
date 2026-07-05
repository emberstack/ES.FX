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
///     MCP write tools for Zendesk uploads (attachment uploads consumed by ticket comments).
///     Namespaced <c>uploads_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskUploadWriteTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IMcpExecutionModeAccessor executionMode)
{
    /// <summary>Uploads a file for attaching to a ticket comment.</summary>
    [McpServerTool(Name = "uploads_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Uploads a file; does NOT attach it. Returns {token, attachments}: 'token' is passed to a ticket comment's " +
        "uploads when creating/updating a ticket; attachments = summary rows (id, file_name, content_type, size) for " +
        "EVERY file on the token so far (read a payload back with attachments_get). Bundle multiple files onto one " +
        "token by passing the returned token back in for each subsequent file. Token valid 60 min. SECURITY: until " +
        "consumed, the file is visible to ANY authenticated user at its content_url even with private attachments " +
        "enabled; once associated with a ticket/post, visibility restricts to users with access. Discard with " +
        "uploads_delete. " +
        "Write op — honors execution mode: rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> Create(
        [Description(
            "Display name when attached (not the source path); may differ from the source name, but its extension " +
            "MUST match the actual content's extension or the recipient's browser/file reader may error on open.")]
        string fileName,
        [Description("File content as base64-encoded bytes.")]
        string contentBase64,
        [Description(
            "Recognized MIME type describing the file (e.g. 'image/png'); a wrong/unrecognized type may cause " +
            "undesired behavior (e.g. browsers blocking playback of mistyped media).")]
        string contentType,
        [Description(
            "Existing upload token to append this file to, for multi-file bundles (pass the first upload's token); " +
            "valid 60 min (optional).")]
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

        // The dry-run echo deliberately omits the base64 payload (frozen convention) — only its decoded length.
        return ZendeskToolInvoker.InvokeWriteAsync(executionMode,
            $"upload file '{fileName}' ({contentType}, {content.Length} bytes)",
            () => UploadAsync(fileName, content, contentType, token, cancellationToken),
            new { fileName, contentType, contentLength = content.Length, token });
    }

    /// <summary>Deletes an unconsumed upload by its token.</summary>
    [McpServerTool(Name = "uploads_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Deletes an unconsumed upload by its token, discarding the file(s) before they attach to a comment. Works " +
        "only while unconsumed and within the 60-min window; once consumed (attached to a comment) it can no longer " +
        "be deleted this way. Returns a completion acknowledgement. " +
        "Write op — honors execution mode: rejected in read-only, simulated (no changes) in dry-run.")]
    public Task<object> Delete(
        [Description(
            "Upload token from uploads_create; deletion works only while unconsumed and within the 60-min window.")]
        string token,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeWriteAsync(executionMode, $"delete upload token '{token}'",
            () =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(token);
                // DELETE /api/v2/uploads/{token} returns 204 with no body — acknowledged by the invoker.
                return zendesk.Api.V2.Uploads[token].DeleteAsync(cancellationToken: cancellationToken);
            }, new { token });

    /// <summary>
    ///     Sends the raw file bytes to <c>POST /api/v2/uploads</c>. The published spec lost the binary request
    ///     body on this operation and never models the <c>token</c> (multi-file chaining) query parameter —
    ///     both are doc-verified
    ///     (https://developer.zendesk.com/documentation/ticketing/managing-tickets/adding-ticket-attachments-with-the-api/
    ///     shows the <c>--data-binary</c> + <c>Content-Type</c> upload;
    ///     https://developer.zendesk.com/api-reference/ticketing/tickets/ticket-attachments/#upload-files shows
    ///     <c>token</c> in the curl example; spec-anomaly ledger rows in
    ///     <c>src/ES.FX.Zendesk/OpenApi/README.md</c>) — so the request is built from the generated builder and
    ///     extended: the stream body is attached with the caller's content type (Zendesk types the attachment
    ///     from the <c>Content-Type</c> header), and the response is parsed as raw JSON rather than through the
    ///     generated model — the spec marks the upload's <c>token</c> and attachment fields read-only, which
    ///     Kiota omits when re-serializing, and losing the token would defeat the whole tool.
    /// </summary>
    private async Task<JsonElement> UploadAsync(string fileName, byte[] content, string contentType, string? token,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var request = zendesk.Api.V2.Uploads.ToPostRequestInformation(cfg =>
            cfg.QueryParameters.Filename = fileName);
        if (token is not null)
        {
            // The generated template ends in a literal query ("?filename={filename}"), so continue it with the
            // RFC 6570 continuation operator ({&token}); the first-parameter form ({?token}) would emit a second '?'.
            request.UrlTemplate += "{&token}";
            request.QueryParameters.Add("token", token);
        }

        using var stream = new MemoryStream(content);
        request.SetStreamContent(stream, contentType);

        var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("upload", out var upload))
            throw new McpException(
                "Zendesk accepted the upload (2xx) but returned no 'upload' token; the file may exist on the server. " +
                "Retrieve or discard before retrying.");
        return BuildLeanConfirmation(upload);
    }

    /// <summary>
    ///     Projects the unwrapped upload object to the lean confirmation <c>{token, attachments}</c>: the token a
    ///     ticket comment consumes, plus attachment summary rows (see <see cref="ZendeskLean.SummarizeEntity" />)
    ///     for every file on the token so far. Zendesk's duplicate top-level <c>attachment</c> — always the file
    ///     just uploaded, repeated as the last element of <c>attachments</c> — is dropped.
    /// </summary>
    private static JsonElement BuildLeanConfirmation(JsonElement upload)
    {
        var source = (JsonObject)JsonNode.Parse(upload.GetRawText())!;
        var confirmation = new JsonObject();
        if (source["token"] is { } token) confirmation["token"] = token.DeepClone();

        var attachments = new JsonArray();
        if (source["attachments"] is JsonArray rows)
            foreach (var row in rows)
                if (row is JsonObject entity)
                    attachments.Add(ZendeskLean.SummarizeEntity("attachments", entity));
        confirmation["attachments"] = attachments;
        return JsonSerializer.SerializeToElement(confirmation);
    }
}