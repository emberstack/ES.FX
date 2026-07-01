using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the operation is still running.
/// </summary>
[PublicAPI]
public record struct InProgress;

/// <summary>
///     Case type indicating that the operation is still running, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the in-progress state.</param>
[PublicAPI]
public record struct InProgress<T>(T Value);