using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Abstractions.Models;

/// <summary>Envelope for a single-attachment response (<c>{ "attachment": { ... } }</c>).</summary>
public sealed record ZendeskAttachmentResponse
{
    [JsonPropertyName("attachment")] public ZendeskAttachment? Attachment { get; init; }
}