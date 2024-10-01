namespace ES.FX.OneOf.Types;

public record struct InProgress;

public record struct InProgress<T>(T Value);