using JetBrains.Annotations;

namespace ES.FX.OneOfExtensions.Types;

[PublicAPI]
public record struct InProgress;

[PublicAPI]
public record struct InProgress<T>(T Value);