namespace ES.FX.ValueRanges;

public record TimeSpanRange : ValueRange<TimeSpan>
{
    public TimeSpanRange(TimeSpan min, TimeSpan max) : base(min, max)
    {
    }

    protected override ValueRange<TimeSpan> CreateFor(TimeSpan min, TimeSpan max) =>
        new TimeSpanRange(min, max);
}