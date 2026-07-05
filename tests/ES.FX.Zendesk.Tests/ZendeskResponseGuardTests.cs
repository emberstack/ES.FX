using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ES.FX.Zendesk.Tests;

public class ZendeskResponseGuardTests
{
    [Fact]
    public async Task Success_Response_Does_Not_Throw()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        await ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Failure_Preserves_Status_And_Body()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("""{"error":"RecordInvalid"}""", Encoding.UTF8, "application/json")
        };

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Contains("RecordInvalid", exception.ResponseBody);
        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task RetryAfter_Delta_Is_Surfaced()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("rate limited")
        };
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
    }

    [Fact]
    public async Task RetryAfter_Date_Form_Is_Converted_To_A_Delay()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(120));

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter.Value, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task RetryAfter_Date_In_The_Past_Clamps_To_Zero()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-30));

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
    }

    [Fact]
    public async Task Error_Body_Is_Capped_At_2048_Bytes()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(new string('x', 10_000))
        };

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(2048, exception.ResponseBody!.Length);
    }

    [Fact]
    public async Task Empty_Body_Yields_Null_ResponseBody()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Null(exception.ResponseBody);
    }

    [Fact]
    public async Task Cancelled_Token_During_Error_Body_Read_Surfaces_OperationCanceledException()
    {
        // The error-body read is best-effort, but cancellation is caller-initiated and must NOT be
        // swallowed into a ZendeskApiException with a null body — the guard rethrows it by design.
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StreamContent(new MemoryStream("""{"error":"RecordInvalid"}"""u8.ToArray()))
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, cts.Token));
    }

    [Fact]
    public async Task Body_Read_Failure_Still_Yields_The_Status_With_A_Null_Body()
    {
        // A body that dies mid-read (e.g. connection reset) must not mask the status-carrying exception.
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StreamContent(new MidReadThrowingStream())
        };

        var exception = await Assert.ThrowsAsync<ZendeskApiException>(() =>
            ZendeskResponseGuard.EnsureSuccessAsync(response, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Null(exception.ResponseBody);
    }

    /// <summary>A read-only stream that yields one chunk and then fails, like a connection reset mid-body.</summary>
    private sealed class MidReadThrowingStream : Stream
    {
        private bool _yieldedFirstChunk;

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

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_yieldedFirstChunk) throw new IOException("Connection reset mid-body.");
            _yieldedFirstChunk = true;
            var chunk = """{"error":"""u8;
            chunk.CopyTo(buffer.Span);
            return ValueTask.FromResult(chunk.Length);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}