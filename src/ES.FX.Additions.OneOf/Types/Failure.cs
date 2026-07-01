using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating an expected, recoverable failure.
/// </summary>
[PublicAPI]
public record struct Failure;

/// <summary>
///     Case type indicating an expected, recoverable failure, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the failure.</param>
[PublicAPI]
public record struct Failure<T>(T Value);