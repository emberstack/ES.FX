namespace ES.FX.ValueRanges;

public record DoubleRange : ValueRange<double>
{
    public DoubleRange(double min, double max) : base(min, max)
    {
    }

    protected override ValueRange<double> CreateFor(double min, double max) => new DoubleRange(min, max);
}