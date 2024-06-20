using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct InProgress;

[PublicAPI]
public record struct InProgress<T>(T Value);