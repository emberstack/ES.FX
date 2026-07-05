using System.Text.Json.Serialization;

namespace ES.FX.NousResearch.HermesAgent.Abstractions.Models;

/// <summary>
///     A chat message, used both in <see cref="HermesAgentChatCompletionRequest.Messages" /> and as the
///     assistant message of a <see cref="HermesAgentChatCompletionChoice" />. The server handles the roles
///     <c>system</c> (concatenated into one ephemeral system prompt; images dropped), <c>user</c> and
///     <c>assistant</c> — other roles are silently ignored.
/// </summary>
public sealed record HermesAgentChatMessage
{
    // Chat roles are not part of the scaffold-owned known-values catalog; the well-known values are kept as
    // private constants and exposed through the factory methods below.
    private const string SystemRole = "system";
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    /// <summary>The message role (<c>system</c>, <c>user</c> or <c>assistant</c>).</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    ///     The message content — a plain string or multimodal content parts (a <see cref="string" /> converts
    ///     implicitly). Assistant messages returned by the server always carry plain-string content.
    /// </summary>
    [JsonPropertyName("content")]
    public HermesAgentMessageContent? Content { get; init; }

    /// <summary>Creates a <c>system</c> message.</summary>
    public static HermesAgentChatMessage FromSystem(HermesAgentMessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new HermesAgentChatMessage { Role = SystemRole, Content = content };
    }

    /// <summary>Creates a <c>user</c> message.</summary>
    public static HermesAgentChatMessage FromUser(HermesAgentMessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new HermesAgentChatMessage { Role = UserRole, Content = content };
    }

    /// <summary>Creates an <c>assistant</c> message (e.g. for client-supplied history).</summary>
    public static HermesAgentChatMessage FromAssistant(HermesAgentMessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new HermesAgentChatMessage { Role = AssistantRole, Content = content };
    }
}