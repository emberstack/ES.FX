using System.Reflection;
using ES.FX.IO;
using JetBrains.Annotations;

namespace ES.FX.Reflection;

/// <summary>
///     Wrapper for a manifest resource embedded in an assembly
///     Provides quick access to content and resource properties
/// </summary>
/// <remarks>
///     Creates a new wrapper for the manifest resource
/// </remarks>
/// <param name="assembly">Source assembly</param>
/// <param name="name">Resource name</param>
[PublicAPI]
public class ManifestResource(Assembly assembly, string name)
{
    /// <summary>
    ///     Gets the resource name
    /// </summary>
    public string Name { get; } = name;


    /// <summary>
    ///     Gets the manifest resource info
    /// </summary>
    public ManifestResourceInfo? Info => assembly.GetManifestResourceInfo(Name);

    /// <summary>
    ///     Returns the stream for the manifest resource
    /// </summary>
    public Stream? GetStream() => assembly.GetManifestResourceStream(Name);


    /// <summary>
    ///     Returns a <see cref="StreamReader"></see> initialized with the manifest resource content <see cref="Stream" />
    /// </summary>
    public StreamReader? GetStreamReader()
    {
        var stream = GetStream();
        return stream is not null ? new StreamReader(stream) : null;
    }


    /// <summary>
    ///     Reads all bytes for the manifest resource
    /// </summary>
    public byte[]? ReadAllBytes()
    {
        using var stream = GetStream();
        return stream?.ToByteArray();
    }


    /// <summary>
    ///     Reads all bytes for the manifest resource
    /// </summary>
    public async Task<byte[]?> ReadAllBytesAsync(CancellationToken cancellation = default)
    {
        await using var stream = GetStream();
        return stream is not null ? await stream.ToByteArrayAsync(cancellation).ConfigureAwait(false) : null;
    }


    /// <summary>
    ///     Returns manifest resource content as text
    /// </summary>
    public string? ReadText()
    {
        using var reader = GetStreamReader();
        return reader?.ReadToEnd();
    }


    /// <summary>
    ///     Returns manifest resource content as text
    /// </summary>
    public async Task<string?> ReadTextAsync(CancellationToken cancellationToken = default)
    {
        using var reader = GetStreamReader();
        if (reader is null) return null;
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}