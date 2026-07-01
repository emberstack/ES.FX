using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class DurationValueTests
{
    [Fact]
    public void DurationValue_CanBe_Equal()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(1, DurationUnit.Day);

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void DurationValue_CanBe_Different()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(1, DurationUnit.Month);

        Assert.NotEqual(a, b);
    }


    [Fact]
    public void DurationValue_CanBe_Compared_SameUnit()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(2, DurationUnit.Day);

        Assert.True(b > a);
    }


    [Fact]
    public void DurationValue_CanBe_Compared_AcrossUnits()
    {
        // Ordering is by absolute length: 2 months (60 days by the 30-day convention) exceeds 1 day.
        Assert.True(new DurationValue(2, DurationUnit.Month) > new DurationValue(1, DurationUnit.Day));
        Assert.True(new DurationValue(1, DurationUnit.Week) > new DurationValue(1, DurationUnit.Day));

        // A year (360 days) is longer than a quarter (90 days).
        Assert.True(new DurationValue(1, DurationUnit.Year) > new DurationValue(1, DurationUnit.Quarter));
    }

    [Fact]
    public void DurationValue_Compare_EqualLengthDifferentUnit_TieBreaksByUnit()
    {
        // 24 hours and 1 day are the same absolute length; ties break by unit so they are ordered, not equal.
        var hours = new DurationValue(24, DurationUnit.Hour);
        var day = new DurationValue(1, DurationUnit.Day);

        Assert.True(hours.CompareTo(day) < 0); // Hour precedes Day
        Assert.NotEqual(hours, day); // record equality stays unit-sensitive
    }

    [Fact]
    public void DurationValue_Compare_IsTotalOrder_AcrossMixedUnits()
    {
        var values = new[]
        {
            new DurationValue(1, DurationUnit.Millennium),
            new DurationValue(1, DurationUnit.Second),
            new DurationValue(500, DurationUnit.Day),
            new DurationValue(1, DurationUnit.Year)
        };

        // Would throw under the old same-unit-only comparison.
        var sorted = values.OrderBy(value => value).ToArray();

        Assert.Equal(DurationUnit.Second, sorted[0].Unit);
        Assert.Equal(DurationUnit.Millennium, sorted[^1].Unit);
    }
}