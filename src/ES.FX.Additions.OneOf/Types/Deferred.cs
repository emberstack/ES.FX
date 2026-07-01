using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

/// <summary>
///     Case type indicating that the operation was accepted but deferred.
/// </summary>
[PublicAPI]
public record struct Deferred;

/// <summary>
///     Case type indicating that the operation was accepted but deferred, carrying a payload.
/// </summary>
/// <typeparam name="T">The type of the payload.</typeparam>
/// <param name="Value">The payload describing the deferral.</param>
[PublicAPI]
public record struct Deferred<T>(T Value);