namespace ES.FX.ValueRanges;

public record DateTimeRange : ValueRange<DateTime>
{
    public DateTimeRange(DateTime min, DateTime max) : base(min, max)
    {
    }

    protected override ValueRange<DateTime> CreateFor(DateTime min, DateTime max) => new DateTimeRange(min, max);
}