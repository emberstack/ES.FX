using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Unknown;

[PublicAPI]
public record struct Unknown<T>(T Value);