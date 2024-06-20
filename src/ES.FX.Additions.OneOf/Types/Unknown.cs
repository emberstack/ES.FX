using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Unknown;

[PublicAPI]
public record struct Unknown<T>(T Value);