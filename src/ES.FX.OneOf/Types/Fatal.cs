using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct Fatal;

[PublicAPI]
public record struct Fatal<T>(T Value);