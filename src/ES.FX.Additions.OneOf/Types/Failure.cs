using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Failure;

[PublicAPI]
public record struct Failure<T>(T Value);