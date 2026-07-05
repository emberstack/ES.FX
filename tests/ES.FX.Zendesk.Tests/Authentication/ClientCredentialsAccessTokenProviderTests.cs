using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ES.FX.Zendesk.Authentication;
using ES.FX.Zendesk.Configuration;
using ES.FX.Zendesk.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ES.FX.Zendesk.Tests.Authentication;

public class ClientCredentialsAccessTokenProviderTests
{
    private const string TokenClientName = "ES.FX.Zendesk.Token";

    private static (ClientCredentialsAccessTokenProvider Provider, CountingHandler Token) Create(
        FakeTimeProvider time, int expiresIn = 1800, TimeSpan? buffer = null)
    {
        var tokenHandler = new CountingHandler(_ => TokenResponse(expiresIn));
        var factory = new StubHttpClientFactory(tokenHandler);
        var options = new StaticOptionsMonitor<ZendeskClientOptions>(new ZendeskClientOptions
        {
            Subdomain = "acme",
            OAuth = new ZendeskOAuthOptions
            {
                ClientId = "cid",
                ClientSecret = "secret",
                Scope = "read",
                ExpiryBuffer = buffer ?? TimeSpan.FromSeconds(60)
            }
        });

        var provider = new ClientCredentialsAccessTokenProvider(
            factory, TokenClientName, options, string.Empty, time,
            NullLogger<ClientCredentialsAccessTokenProvider>.Instance);

        return (provider, tokenHandler);
    }

    private static HttpResponseMessage TokenResponse(int expiresIn) =>
        new(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(new
            {
                access_token = $"tok-{Guid.NewGuid():N}",
                token_type = "bearer",
                scope = "read",
                expires_in = expiresIn
            })
        };

    [Fact]
    public async Task Token_Is_Fetched_Once_And_Reused_Under_Concurrency()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, token) = Create(new FakeTimeProvider());

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => provider.GetAccessTokenAsync(cancellationToken: cancellationToken)));

        Assert.Equal(1, token.Calls); // no stampede — fetched exactly once
        Assert.All(results, t => Assert.Equal(results[0], t)); // everyone got the same token
    }

    [Fact]
    public async Task Token_Is_Reused_Within_Validity_And_Refreshed_After_Buffered_Expiry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var (provider, token) = Create(time, 300, TimeSpan.FromSeconds(60));

        await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        Assert.Equal(1, token.Calls);

        // Effective expiry = 300 - 60 = 240s. Still valid at 239s → reuse.
        time.Advance(TimeSpan.FromSeconds(239));
        await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        Assert.Equal(1, token.Calls);

        // Past the buffered expiry (241s > 240s) → refresh.
        time.Advance(TimeSpan.FromSeconds(2));
        await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        Assert.Equal(2, token.Calls);
    }

    [Fact]
    public async Task ForceRefresh_Bypasses_The_Cache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (provider, token) = Create(new FakeTimeProvider());

        await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);
        await provider.GetAccessTokenAsync(true, cancellationToken);

        Assert.Equal(2, token.Calls);
    }

    [Fact]
    public async Task Concurrent_Force_Refreshes_Issue_A_Single_Token_Request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var handler = new GatedTokenHandler();
        var options = new StaticOptionsMonitor<ZendeskClientOptions>(new ZendeskClientOptions
        {
            Subdomain = "acme",
            OAuth = new ZendeskOAuthOptions { ClientId = "cid", ClientSecret = "secret", Scope = "read" }
        });
        var provider = new ClientCredentialsAccessTokenProvider(
            new StubHttpClientFactory(handler), TokenClientName, options, string.Empty,
            new FakeTimeProvider(), NullLogger<ClientCredentialsAccessTokenProvider>.Instance);

        // Prime the cache (handler call #1, ungated).
        var initial = await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);

        // Simulate a 401 storm: many concurrent force-refreshers. Materializing the sequence runs each call
        // synchronously up to its lock wait, so every caller captures the same stale token before the (gated)
        // refresh completes. The fix must make them all reuse the first refresh's token — one request, not N.
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => provider.GetAccessTokenAsync(true, cancellationToken))
            .ToArray();
        handler.Open();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(2, handler.Calls); // one prime + exactly one refresh, not one-per-caller
        Assert.All(results, token => Assert.Equal(results[0], token));
        Assert.NotEqual(initial, results[0]); // the forced refresh did supersede the primed token
    }

    [Fact]
    public async Task Token_Request_Uses_A_Json_Body_With_Client_Credentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var capturing = new CapturingHandler(() => TokenResponse(1800));
        var options = new StaticOptionsMonitor<ZendeskClientOptions>(new ZendeskClientOptions
        {
            Subdomain = "acme",
            OAuth = new ZendeskOAuthOptions { ClientId = "cid", ClientSecret = "secret", Scope = "read" }
        });
        var provider = new ClientCredentialsAccessTokenProvider(
            new StubHttpClientFactory(capturing), TokenClientName, options, string.Empty,
            new FakeTimeProvider(), NullLogger<ClientCredentialsAccessTokenProvider>.Instance);

        await provider.GetAccessTokenAsync(cancellationToken: cancellationToken);

        // Zendesk's token endpoint requires a JSON body (NOT application/x-www-form-urlencoded).
        Assert.Equal("application/json", capturing.ContentType);
        using var body = JsonDocument.Parse(capturing.Body!);
        Assert.Equal("client_credentials", body.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal("cid", body.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("secret", body.RootElement.GetProperty("client_secret").GetString());
    }

    [Fact]
    public async Task Throws_ZendeskApiException_When_Token_Endpoint_Fails()
    {
        var tokenHandler = new CountingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { error = "invalid_client" })
            });
        var options = new StaticOptionsMonitor<ZendeskClientOptions>(new ZendeskClientOptions
        {
            Subdomain = "acme",
            OAuth = new ZendeskOAuthOptions { ClientId = "cid", ClientSecret = "bad" }
        });
        var provider = new ClientCredentialsAccessTokenProvider(
            new StubHttpClientFactory(tokenHandler), TokenClientName, options, string.Empty,
            new FakeTimeProvider(), NullLogger<ClientCredentialsAccessTokenProvider>.Instance);

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(async () =>
            await provider.GetAccessTokenAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    /// <summary>
    ///     A token handler that returns a distinct token per call and gates the second request (the storm refresh)
    ///     until <see cref="Open" /> is called, so a concurrent-refresh scenario is deterministic.
    /// </summary>
    private sealed class GatedTokenHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public void Open() => _gate.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _calls);
            if (n == 2) await _gate.Task.ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = JsonContent.Create(new
                {
                    access_token = $"tok-{n}",
                    token_type = "bearer",
                    scope = "read",
                    expires_in = 1800
                })
            };
        }
    }

    /// <summary>A token handler that records the outgoing request's content type and body.</summary>
    private sealed class CapturingHandler(Func<HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string? ContentType { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            if (request.Content is not null)
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return responder();
        }
    }
}