using JetBrains.Annotations;

namespace ES.FX.Problems;

[PublicAPI]
public record Problem(string Type, string? Title = null, string? Detail = null, string? Instance = null);