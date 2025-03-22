using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Types;

[PublicAPI]
public record struct Interrupted;

[PublicAPI]
public record struct Interrupted<T>(T Value);