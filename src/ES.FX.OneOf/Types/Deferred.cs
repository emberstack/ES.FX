using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Deferred;

[PublicAPI]
public record struct Deferred<T>(T Value);