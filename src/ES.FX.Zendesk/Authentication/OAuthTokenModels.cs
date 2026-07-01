using System.Text.Json.Serialization;

namespace ES.FX.Zendesk.Authentication;

/// <summary>
///     The OAuth 2.0 <c>client_credentials</c> token request body sent to Zendesk (as JSON, per Zendesk's token
///     endpoint contract).
/// </summary>
internal sealed record ClientCredentialsTokenRequest
{
    [JsonPropertyName("grant_type")] public string GrantType { get; init; } = "client_credentials";

    [JsonPropertyName("client_id")] public required string ClientId { get; init; }

    [JsonPropertyName("client_secret")] public required string ClientSecret { get; init; }

    [JsonPropertyName("scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scope { get; init; }

    [JsonPropertyName("expires_in")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiresIn { get; init; }
}

/// <summary>
///     The OAuth 2.0 token endpoint response (RFC 6749 §5.1). Zendesk's <c>client_credentials</c> grant returns no
///     refresh token.
/// </summary>
internal sealed record ClientCredentialsTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")] public string? TokenType { get; init; }

    [JsonPropertyName("scope")] public string? Scope { get; init; }

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
}