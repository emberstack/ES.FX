using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the state could not be determined.
/// </summary>
[PublicAPI]
public record struct Unknown;

/// <summary>
///     Case type indicating that the state could not be determined, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the unknown state.</param>
[PublicAPI]
public record struct Unknown<T>(T Value);