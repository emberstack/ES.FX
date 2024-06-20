using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Interrupted;

[PublicAPI]
public record struct Interrupted<T>(T Value);