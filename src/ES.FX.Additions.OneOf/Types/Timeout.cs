using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Timeout;

[PublicAPI]
public record struct Timeout<T>(T Value);