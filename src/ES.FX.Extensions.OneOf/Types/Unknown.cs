using JetBrains.Annotations;

namespace ES.FX.Extensions.OneOf.Types;

[PublicAPI]
public record struct Unknown;

[PublicAPI]
public record struct Unknown<T>(T Value);