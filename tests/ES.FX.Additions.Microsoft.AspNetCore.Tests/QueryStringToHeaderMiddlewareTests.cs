using ES.FX.Additions.Microsoft.AspNetCore.Middleware;

namespace ES.FX.Additions.Microsoft.AspNetCore.Tests;

/// <summary>
///     Functional regression coverage for <see cref="QueryStringToHeaderMiddleware" />. Uses an in-memory
///     TestServer whose terminal endpoint echoes request headers back as <c>Echo-*</c> response headers, so
///     each test asserts exactly which query keys the middleware promoted into request headers.
/// </summary>
public class QueryStringToHeaderMiddlewareTests
{
    private const string Prefix = QueryStringToHeaderMiddleware.Prefix; // "X-Header-"

    /// <summary>Returns the echoed request-header value the middleware set for <paramref name="headerName" />, or null.</summary>
    private static async Task<string?> GetEchoedHeaderAsync(string query, string headerName)
    {
        using var server = TestServerFactory.CreateWithMiddleware<QueryStringToHeaderMiddleware>();
        var response = await server.CreateClient().GetAsync("/" + query, TestContext.Current.CancellationToken);
        var echoKey = TestServerFactory.EchoResponsePrefix + headerName;
        return response.Headers.TryGetValues(echoKey, out var values) ? string.Join(",", values) : null;
    }

    [Fact]
    public void Prefix_HasExpectedValue()
    {
        // Guards the public contract: consumers and clients depend on this exact prefix.
        Assert.Equal("X-Header-", QueryStringToHeaderMiddleware.Prefix);
    }

    [Fact]
    public async Task ValidHeaderName_IsPromotedToRequestHeader()
    {
        var value = await GetEchoedHeaderAsync($"?{Prefix}Correlation-Id=abc123", "Correlation-Id");
        Assert.Equal("abc123", value);
    }

    [Fact]
    public async Task PrefixMatch_IsCaseInsensitive()
    {
        // The Where clause uses OrdinalIgnoreCase against the prefix.
        var value = await GetEchoedHeaderAsync("?x-header-Tenant=acme", "Tenant");
        Assert.Equal("acme", value);
    }

    [Fact]
    public async Task NonPrefixedQueryKey_IsNotPromoted()
    {
        var value = await GetEchoedHeaderAsync("?SomeOther=value", "SomeOther");
        Assert.Null(value);
    }

    [Fact]
    public async Task EmptyHeaderName_AfterPrefix_IsIgnored()
    {
        // "?X-Header-=value" -> header key is empty -> IsNullOrWhiteSpace short-circuits.
        using var server = TestServerFactory.CreateWithMiddleware<QueryStringToHeaderMiddleware>();
        var response = await server.CreateClient().GetAsync($"/?{Prefix}=value", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
        // No empty-named header should have been created; nothing to assert beyond a clean pass-through.
    }

    [Theory]
    [InlineData("!")]
    [InlineData("#")]
    [InlineData("$")]
    [InlineData("%")]
    [InlineData("&")]
    [InlineData("*")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("^")]
    [InlineData("_")]
    [InlineData("|")]
    [InlineData("~")]
    public async Task HeaderNamesWithAllowedTchars_ArePromoted(string special)
    {
        var name = "a" + special + "b";
        var value = await GetEchoedHeaderAsync($"?{Prefix}{Uri.EscapeDataString(name)}=ok", name);
        Assert.Equal("ok", value);
    }

    [Theory]
    [InlineData("a b")] // space is not a tchar
    [InlineData("a(b")] // parenthesis
    [InlineData("a@b")] // @ separator
    [InlineData("a:b")] // colon separator
    [InlineData("a/b")] // slash
    [InlineData("a=b")] // equals — cannot actually reach as name, but validates rejection logic
    public async Task HeaderNamesWithInvalidChars_AreRejected(string name)
    {
        var value = await GetEchoedHeaderAsync($"?{Prefix}{Uri.EscapeDataString(name)}=payload", name);
        Assert.Null(value);
    }

    [Fact]
    public async Task HeaderValueWithCarriageReturn_IsRejected()
    {
        // CR in the value would enable header/response splitting — must be dropped.
        var injected = Uri.EscapeDataString("value\rInjected: evil");
        var value = await GetEchoedHeaderAsync($"?{Prefix}Safe={injected}", "Safe");
        Assert.Null(value);
    }

    [Fact]
    public async Task HeaderValueWithLineFeed_IsRejected()
    {
        var injected = Uri.EscapeDataString("value\nInjected: evil");
        var value = await GetEchoedHeaderAsync($"?{Prefix}Safe={injected}", "Safe");
        Assert.Null(value);
    }

    [Fact]
    public async Task HeaderValueWithNullByte_IsRejected()
    {
        var injected = Uri.EscapeDataString("value\0tail");
        var value = await GetEchoedHeaderAsync($"?{Prefix}Safe={injected}", "Safe");
        Assert.Null(value);
    }

    [Fact]
    public async Task NoInjectedHeaderLeaksIntoResponse_OnCrlfAttempt()
    {
        // Belt-and-suspenders: assert the injected header name never materialized anywhere.
        var injected = Uri.EscapeDataString("value\r\nInjected: evil");
        using var server = TestServerFactory.CreateWithMiddleware<QueryStringToHeaderMiddleware>();
        var response = await server.CreateClient()
            .GetAsync($"/?{Prefix}Safe={injected}", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.TryGetValues("Echo-Injected", out _));
        Assert.False(response.Headers.TryGetValues("Injected", out _));
    }

    [Fact]
    public async Task CleanValue_PassesThroughEvenWhenOtherInvalidPairsPresent()
    {
        // A valid pair should still be promoted independently of an invalid sibling pair.
        var injected = Uri.EscapeDataString("bad\rvalue");
        using var server = TestServerFactory.CreateWithMiddleware<QueryStringToHeaderMiddleware>();
        var response = await server.CreateClient()
            .GetAsync($"/?{Prefix}Good=clean&{Prefix}Bad={injected}", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Echo-Good", out var good));
        Assert.Equal("clean", string.Join(",", good));
        Assert.False(response.Headers.TryGetValues("Echo-Bad", out _));
    }

    [Fact]
    public async Task WhitespaceOnlyHeaderName_IsIgnored()
    {
        // "?X-Header-%20=value" -> header key " " -> IsNullOrWhiteSpace -> skipped.
        var value = await GetEchoedHeaderAsync($"?{Prefix}%20=value", " ");
        Assert.Null(value);
    }
}