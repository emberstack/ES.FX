namespace ES.FX.OneOf.Types;

public record struct Failure;

public record struct Failure<T>(T Value);