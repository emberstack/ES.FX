using JetBrains.Annotations;

namespace ES.FX.OneOf.Types;

[PublicAPI]
public record struct Canceled;

[PublicAPI]
public record struct Canceled<T>(T Value);