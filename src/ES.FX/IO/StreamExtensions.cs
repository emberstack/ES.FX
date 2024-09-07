using JetBrains.Annotations;
using static System.ArgumentNullException;

namespace ES.FX.IO;

[PublicAPI]
public static class StreamExtensions
{
    /// <summary>
    ///     Reads all bytes from the stream and returns them as a byte array
    /// </summary>
    public static byte[] ToByteArray(this Stream stream)
    {
        ThrowIfNull(stream);

        if (stream is MemoryStream directMemoryStream)
            return directMemoryStream.ToArray();

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    ///     Reads all bytes from the stream asynchronously and returns them as a byte array
    /// </summary>
    public static async Task<byte[]> ToByteArrayAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        ThrowIfNull(stream);

        if (stream is MemoryStream directMemoryStream)
            return directMemoryStream.ToArray();

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }
}