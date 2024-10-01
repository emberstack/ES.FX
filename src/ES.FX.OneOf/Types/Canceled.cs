namespace ES.FX.OneOf.Types;

public record struct Canceled;

public record struct Canceled<T>(T Value);