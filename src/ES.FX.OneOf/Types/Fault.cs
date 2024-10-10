using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Fault;

[PublicAPI]
public record struct Fault<T>(T Value);