using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the operation was interrupted before completing.
/// </summary>
[PublicAPI]
public record struct Interrupted;

/// <summary>
///     Case type indicating that the operation was interrupted before completing, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the interruption.</param>
[PublicAPI]
public record struct Interrupted<T>(T Value);