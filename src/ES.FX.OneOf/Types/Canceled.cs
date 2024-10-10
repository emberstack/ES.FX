using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Canceled;

[PublicAPI]
public record struct Canceled<T>(T Value);