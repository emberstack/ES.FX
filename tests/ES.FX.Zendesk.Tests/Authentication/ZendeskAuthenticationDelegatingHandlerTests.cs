using System.Net;
using System.Text;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Authentication;

namespace ES.FX.Zendesk.Tests.Authentication;

public class ZendeskAuthenticationDelegatingHandlerTests
{
    [Fact]
    public async Task Applies_Bearer_Token_To_Outgoing_Request()
    {
        var provider = new StubTokenProvider("tok-abc");
        var inner = new SequencedHandler(HttpStatusCode.OK);
        var handler = new ZendeskAuthenticationDelegatingHandler(provider) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://acme.zendesk.com/api/v2/users/me.json");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("Bearer", inner.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("tok-abc", inner.LastRequest.Headers.Authorization.Parameter);
        Assert.Equal(1, inner.Calls);
        Assert.False(provider.LastForceRefresh);
    }

    [Fact]
    public async Task On_401_Retries_Once_With_The_Refreshed_Token()
    {
        // The provider hands out a fresh token per call ("tok-1", then the forced-refresh "tok-2").
        var provider = new SequencedTokenProvider();
        // The server rejects everything except the refreshed token, so the retry only succeeds if the handler
        // actually re-applies the newly refreshed token (not the stale one it sent first).
        var inner = new TokenAwareHandler("tok-2");
        var handler = new ZendeskAuthenticationDelegatingHandler(provider) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://acme.zendesk.com/api/v2/users/me.json");
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls); // retried exactly once
        Assert.Equal(2, provider.CallCount); // initial + forced refresh
        Assert.True(provider.ForceRefreshRequested);
        Assert.Equal("tok-1", inner.AuthParameters[0]); // first attempt carried the initial token
        Assert.Equal("tok-2", inner.AuthParameters[1]); // retry carried the REFRESHED token
    }

    [Fact]
    public async Task On_401_Retry_Replays_Buffered_Request_Content()
    {
        var provider = new SequencedTokenProvider();
        var inner = new ContentCapturingHandler("tok-2");
        var handler = new ZendeskAuthenticationDelegatingHandler(provider) { InnerHandler = inner };
        using var invoker = new HttpMessageInvoker(handler);

        // A forward-only stream: without up-front buffering, the first attempt consumes it and the 401 retry
        // would throw (or silently send an empty body) instead of replaying the payload.
        var payload = "{\"ticket\":{\"subject\":\"hello\"}}"u8.ToArray();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://acme.zendesk.com/api/v2/tickets.json")
        {
            Content = new StreamContent(new NonSeekableReadOnlyStream(payload))
        };
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Bodies.Count);
        Assert.Equal("{\"ticket\":{\"subject\":\"hello\"}}", inner.Bodies[0]);
        Assert.Equal(inner.Bodies[0], inner.Bodies[1]); // the retry re-sent the SAME body, not an empty one
    }

    private sealed class StubTokenProvider(string token) : IZendeskAccessTokenProvider
    {
        public int CallCount { get; private set; }
        public bool LastForceRefresh { get; private set; }
        public bool ForceRefreshRequested { get; private set; }

        public Task<string> GetAccessTokenAsync(bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastForceRefresh = forceRefresh;
            if (forceRefresh) ForceRefreshRequested = true;
            return Task.FromResult(token);
        }
    }

    private sealed class SequencedHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int _index;

        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            var status = statuses[Math.Min(_index++, statuses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class SequencedTokenProvider : IZendeskAccessTokenProvider
    {
        public int CallCount { get; private set; }

        public bool ForceRefreshRequested { get; private set; }

        public Task<string> GetAccessTokenAsync(bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (forceRefresh) ForceRefreshRequested = true;
            return Task.FromResult($"tok-{++CallCount}");
        }
    }

    private sealed class TokenAwareHandler(string acceptToken) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string?> AuthParameters { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            var parameter = request.Headers.Authorization?.Parameter;
            AuthParameters.Add(parameter);
            var status = parameter == acceptToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    /// <summary>
    ///     Rejects every token except <paramref name="acceptToken" /> with 401, and reads the request content the
    ///     way the real wire does (<see cref="HttpContent.CopyToAsync(Stream)" />, no implicit buffering) so a
    ///     consumed forward-only stream fails instead of being silently re-buffered by the test itself.
    /// </summary>
    private sealed class ContentCapturingHandler(string acceptToken) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            if (request.Content is not null)
                await request.Content.CopyToAsync(buffer, cancellationToken);
            Bodies.Add(Encoding.UTF8.GetString(buffer.ToArray()));

            var status = request.Headers.Authorization?.Parameter == acceptToken
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized;
            return new HttpResponseMessage(status);
        }
    }

    private sealed class NonSeekableReadOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data, false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}