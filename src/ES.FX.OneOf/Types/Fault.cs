namespace ES.FX.OneOf.Types;

public record struct Fault;

public record struct Fault<T>(T Value);