namespace ES.FX.ValueRanges;

public record DateTimeOffsetRange : ValueRange<DateTimeOffset>
{
    public DateTimeOffsetRange(DateTimeOffset min, DateTimeOffset max) : base(min, max)
    {
    }

    protected override ValueRange<DateTimeOffset> CreateFor(DateTimeOffset min, DateTimeOffset max) =>
        new DateTimeOffsetRange(min, max);
}