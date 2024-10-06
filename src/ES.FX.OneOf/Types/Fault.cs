using JetBrains.Annotations;

namespace ES.FX.OneOf.Types;

[PublicAPI]
public record struct Fault;

[PublicAPI]
public record struct Fault<T>(T Value);