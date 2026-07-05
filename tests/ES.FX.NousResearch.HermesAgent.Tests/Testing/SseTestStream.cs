using System.Text;

namespace ES.FX.NousResearch.HermesAgent.Tests.Testing;

/// <summary>
///     A read-only stream for SSE lifetime tests. Serves a fixed payload read-by-read and then either reports
///     end-of-stream or blocks until the pending read is cancelled (simulating a keepalive-only live stream).
///     Records disposal so response-lifetime behavior (consumer abandonment) can be asserted.
/// </summary>
internal sealed class SseTestStream(string payload, bool blockAfterPayload = false) : Stream
{
    private readonly byte[] _payload = Encoding.UTF8.GetBytes(payload);
    private int _position;

    /// <summary>Whether the stream has been disposed (i.e. the owning HTTP response was disposed).</summary>
    public bool Disposed { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _payload.Length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (_position < _payload.Length)
        {
            var count = Math.Min(buffer.Length, _payload.Length - _position);
            _payload.AsMemory(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        if (blockAfterPayload)
            // Completes only by cancellation — a token-drop regression turns this into a hang, which the
            // caller bounds with a timeout so the test fails instead of blocking the run.
            await Task.Delay(Timeout.Infinite, cancellationToken);

        return 0;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}