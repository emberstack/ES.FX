using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the operation exceeded its time budget.
/// </summary>
[PublicAPI]
public record struct TimedOut;

/// <summary>
///     Case type indicating that the operation exceeded its time budget, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the timeout.</param>
[PublicAPI]
public record struct TimedOut<T>(T Value);