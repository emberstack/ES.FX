using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the operation was canceled.
/// </summary>
[PublicAPI]
public record struct Canceled;

/// <summary>
///     Case type indicating that the operation was canceled, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the cancellation.</param>
[PublicAPI]
public record struct Canceled<T>(T Value);