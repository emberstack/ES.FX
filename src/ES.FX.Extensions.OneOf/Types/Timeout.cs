using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Types;

[PublicAPI]
public record struct Timeout;

[PublicAPI]
public record struct Timeout<T>(T Value);