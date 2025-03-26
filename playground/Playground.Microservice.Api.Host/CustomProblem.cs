using ES.FX.Problems;

public record CustomProblem : Problem
{
    public CardIssuer Issuer { get; init; }
}