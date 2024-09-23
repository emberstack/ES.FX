namespace ES.FX.ValueRanges;

public record IntRange : ValueRange<int>
{
    public IntRange(int min, int max) : base(min, max)
    {
    }

    protected override ValueRange<int> CreateFor(int min, int max) => new IntRange(min, max);
}