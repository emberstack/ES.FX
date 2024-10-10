using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Interrupted;

[PublicAPI]
public record struct Interrupted<T>(T Value);