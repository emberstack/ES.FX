using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating an unrecoverable error.
/// </summary>
[PublicAPI]
public record struct Fatal;

/// <summary>
///     Case type indicating an unrecoverable error, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the error.</param>
[PublicAPI]
public record struct Fatal<T>(T Value);