namespace ES.FX.OneOf.Types;

public record struct Fatal;

public record struct Fatal<T>(T Value);