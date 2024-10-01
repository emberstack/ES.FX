namespace ES.FX.OneOf.Types;

public record struct Timeout;

public record struct Timeout<T>(T Value);