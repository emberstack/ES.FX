using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Types;

[PublicAPI]
public record struct Deferred;

[PublicAPI]
public record struct Deferred<T>(T Value);