using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class ValueRangeTests
{
    [Fact]
    public void Ctor_Exact_MinEqualsMax()
    {
        var range = new ValueRange<int>(5);
        Assert.Equal(5, range.Min);
        Assert.Equal(5, range.Max);
        Assert.True(range.IsExact());
    }

    [Fact]
    public void Ctor_MinMax_SetsValues()
    {
        var range = new ValueRange<int>(1, 10);
        Assert.Equal(1, range.Min);
        Assert.Equal(10, range.Max);
    }

    [Fact]
    public void Ctor_MinGreaterThanMax_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ValueRange<int>(10, 1));
    }

    [Fact]
    public void Ctor_CopyRange_CopiesBounds()
    {
        var original = new ValueRange<int>(2, 8);
        var copy = new ValueRange<int>(original);
        Assert.Equal(2, copy.Min);
        Assert.Equal(8, copy.Max);
    }

    // ---- Contains ----

    [Theory]
    [InlineData(1, true)] // lower boundary
    [InlineData(10, true)] // upper boundary
    [InlineData(5, true)] // inside
    [InlineData(0, false)] // below
    [InlineData(11, false)] // above
    public void Contains_Boundaries(int value, bool expected)
    {
        var range = new ValueRange<int>(1, 10);
        Assert.Equal(expected, range.Contains(value));
    }

    // ---- Intersect ----

    [Fact]
    public void Intersect_Overlapping_ReturnsOverlap()
    {
        var a = new ValueRange<int>(1, 10);
        var b = new ValueRange<int>(5, 15);
        var result = a.Intersect(b);

        Assert.NotNull(result);
        Assert.Equal(5, result!.Value.Min);
        Assert.Equal(10, result.Value.Max);
    }

    [Fact]
    public void Intersect_Disjoint_ReturnsNull()
    {
        var a = new ValueRange<int>(1, 5);
        var b = new ValueRange<int>(10, 20);
        Assert.Null(a.Intersect(b));
    }

    [Fact]
    public void Intersect_TouchingAtEndpoint_ReturnsExactRange()
    {
        var a = new ValueRange<int>(1, 5);
        var b = new ValueRange<int>(5, 10);
        var result = a.Intersect(b);

        Assert.NotNull(result);
        Assert.Equal(5, result!.Value.Min);
        Assert.Equal(5, result.Value.Max);
        Assert.True(result.Value.IsExact());
    }

    [Fact]
    public void Intersect_Contained_ReturnsInnerRange()
    {
        var outer = new ValueRange<int>(0, 100);
        var inner = new ValueRange<int>(20, 30);
        var result = outer.Intersect(inner);

        Assert.NotNull(result);
        Assert.Equal(20, result!.Value.Min);
        Assert.Equal(30, result.Value.Max);
    }

    // ---- IsExact ----

    [Fact]
    public void IsExact_NonExact_False()
    {
        Assert.False(new ValueRange<int>(1, 2).IsExact());
    }

    // ---- CompareTo ----

    [Fact]
    public void CompareTo_OrdersByMinThenMax()
    {
        var a = new ValueRange<int>(1, 5);
        var b = new ValueRange<int>(1, 10);
        var c = new ValueRange<int>(2, 3);

        Assert.True(a.CompareTo(b) < 0); // same min, smaller max
        Assert.True(b.CompareTo(a) > 0);
        Assert.True(a.CompareTo(c) < 0); // smaller min wins
        Assert.Equal(0, a.CompareTo(new ValueRange<int>(1, 5)));
    }

    [Fact]
    public void CompareTo_Object_Null_ReturnsPositive()
    {
        IComparable range = new ValueRange<int>(1, 5);
        Assert.True(range.CompareTo(null) > 0);
    }

    [Fact]
    public void CompareTo_Object_SameType_Delegates()
    {
        IComparable range = new ValueRange<int>(1, 5);
        Assert.Equal(0, range.CompareTo(new ValueRange<int>(1, 5)));
    }

    [Fact]
    public void CompareTo_Object_WrongType_Throws()
    {
        IComparable range = new ValueRange<int>(1, 5);
        Assert.Throws<ArgumentException>(() => range.CompareTo("not a range"));
    }

    // ---- ToString ----

    [Fact]
    public void ToString_Formats_Bracketed()
    {
        Assert.Equal("[1, 10]", new ValueRange<int>(1, 10).ToString());
    }

    // ---- record equality ----

    [Fact]
    public void Equality_SameBounds_Equal()
    {
        Assert.Equal(new ValueRange<int>(1, 10), new ValueRange<int>(1, 10));
        Assert.NotEqual(new ValueRange<int>(1, 10), new ValueRange<int>(1, 11));
    }
}