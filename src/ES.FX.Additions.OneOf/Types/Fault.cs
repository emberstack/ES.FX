using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating an error condition.
/// </summary>
[PublicAPI]
public record struct Fault;

/// <summary>
///     Case type indicating an error condition, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the error.</param>
[PublicAPI]
public record struct Fault<T>(T Value);