using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class DurationValueBehaviorTests
{
    // ---- Negative guards ----

    [Fact]
    public void Ctor_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DurationValue(-1, DurationUnit.Day));
    }

    [Fact]
    public void InitSetter_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _ = new DurationValue(1, DurationUnit.Day) with { Value = -5 });
    }

    [Fact]
    public void Ctor_ZeroValue_Allowed()
    {
        var duration = new DurationValue(0, DurationUnit.Second);
        Assert.Equal(0, duration.Value);
        Assert.Equal(DurationUnit.Second, duration.Unit);
    }

    // ---- ToString pluralization ----

    [Fact]
    public void ToString_Singular_NoPluralSuffix()
    {
        Assert.Equal("1 Day", new DurationValue(1, DurationUnit.Day).ToString());
        Assert.Equal("1 Second", new DurationValue(1, DurationUnit.Second).ToString());
    }

    [Fact]
    public void ToString_Plural_AddsS()
    {
        Assert.Equal("7 Days", new DurationValue(7, DurationUnit.Day).ToString());
        Assert.Equal("0 Days", new DurationValue(0, DurationUnit.Day).ToString());
        Assert.Equal("2 Weeks", new DurationValue(2, DurationUnit.Week).ToString());
    }

    [Fact]
    public void ToString_Century_SpecialPlural_Centuries()
    {
        Assert.Equal("1 Century", new DurationValue(1, DurationUnit.Century).ToString());
        Assert.Equal("3 Centuries", new DurationValue(3, DurationUnit.Century).ToString());
    }

    [Fact]
    public void ToString_Millennium_SpecialPlural_Millennia()
    {
        Assert.Equal("1 Millennium", new DurationValue(1, DurationUnit.Millennium).ToString());
        Assert.Equal("5 Millennia", new DurationValue(5, DurationUnit.Millennium).ToString());
    }

    // ---- ToNanoseconds edge via CompareTo (large BigInteger values) ----

    [Fact]
    public void CompareTo_LargeValues_UsesBigIntegerWithoutOverflow()
    {
        // long.MaxValue millennia would overflow Int64 nanoseconds; BigInteger keeps it correct.
        var huge = new DurationValue(long.MaxValue, DurationUnit.Millennium);
        var small = new DurationValue(1, DurationUnit.Nanosecond);

        Assert.True(huge > small);
        Assert.True(huge.CompareTo(small) > 0);
    }

    [Fact]
    public void CompareTo_NonGeneric_Null_ReturnsPositive()
    {
        IComparable duration = new DurationValue(1, DurationUnit.Day);
        Assert.True(duration.CompareTo(null) > 0);
    }

    [Fact]
    public void CompareTo_NonGeneric_WrongType_Throws()
    {
        IComparable duration = new DurationValue(1, DurationUnit.Day);
        Assert.Throws<ArgumentException>(() => duration.CompareTo("not a duration"));
    }

    [Fact]
    public void Operators_LessThanOrEqual_GreaterThanOrEqual()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(2, DurationUnit.Day);
        var aCopy = new DurationValue(1, DurationUnit.Day);

        Assert.True(a < b);
        Assert.True(a <= aCopy);
        Assert.True(b >= a);
        Assert.True(a >= aCopy);
    }
}