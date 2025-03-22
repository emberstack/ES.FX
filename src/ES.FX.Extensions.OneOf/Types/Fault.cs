using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Types;

[PublicAPI]
public record struct Fault;

[PublicAPI]
public record struct Fault<T>(T Value);