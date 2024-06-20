using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Deferred;

[PublicAPI]
public record struct Deferred<T>(T Value);