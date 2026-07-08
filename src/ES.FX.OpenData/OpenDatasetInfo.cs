using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>
///     Describes a single OpenData dataset: its identity, the data edition it ships, and the provenance and
///     license of the underlying data. Exposed at runtime via <see cref="IOpenData.Datasets" />
///     so hosts can log, diagnose, and audit which editions are deployed.
/// </summary>
[PublicAPI]
public sealed record OpenDatasetInfo
{
    /// <summary>The human-readable dataset name (e.g. <c>"Countries"</c>, <c>"SIRUTA"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The edition of the shipped data (e.g. <c>"2024"</c>, <c>"2025-12"</c>). Consumers that persist codes
    ///     should gate on this value; a change in edition is always at least a minor package version bump.
    /// </summary>
    public required string Edition { get; init; }

    /// <summary>The publisher / authoritative source of the data (e.g. <c>"INS"</c>, <c>"ISO 3166-1"</c>).</summary>
    public required string Source { get; init; }

    /// <summary>The URL of the authoritative source, when one exists.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>The license under which the underlying data is redistributed.</summary>
    public required string License { get; init; }

    /// <summary>The standard the dataset implements, when applicable (e.g. <c>"ISO 3166-1"</c>, <c>"SIRUTA"</c>).</summary>
    public string? Standard { get; init; }
}
