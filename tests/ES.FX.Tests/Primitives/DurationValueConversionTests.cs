using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

/// <summary>
///     Pins the absolute-length conversion factor of every <see cref="DurationUnit" /> so that a
///     corrupted <c>NanosecondsPer(unit)</c> factor is detected via observable ordering.
/// </summary>
/// <remarks>
///     Each factor is pinned two ways against a reference unit whose factor is exercised
///     independently, forming a chain back to <see cref="DurationUnit.Nanosecond" /> (== 1):
///     <list type="bullet">
///         <item>An exact-length equality: N of the unit is the same absolute length as M of a smaller
///         reference unit, so <c>CompareTo</c> ties on length and only the unit ordinal breaks it.</item>
///         <item>An off-by-one strictness: (N*value ± reference) crosses the boundary, so the length
///         comparison flips. A wrong factor breaks at least one of these.</item>
///     </list>
///     Expected boundaries are literal integer relationships from the unit definitions
///     (1 Tick == 100 ns, 1 Minute == 60 s, ...), never recomputed via the code under test.
/// </remarks>
public class DurationValueConversionTests
{
    private static DurationValue D(long value, DurationUnit unit) => new(value, unit);

    /// <summary>
    ///     (unit, count) has the same absolute length as (reference, referenceCount).
    ///     Because lengths tie, <c>CompareTo</c> must fall through to the unit ordinal, and the
    ///     length-adjacent strict comparisons below/above must flip. This nails the exact factor.
    /// </summary>
    [Theory]
    // Tick: 1 Tick == 100 Nanoseconds
    [InlineData(DurationUnit.Tick, 1, DurationUnit.Nanosecond, 100)]
    // Microsecond: 1 Microsecond == 1_000 Nanoseconds
    [InlineData(DurationUnit.Microsecond, 1, DurationUnit.Nanosecond, 1_000)]
    // Millisecond: 1 Millisecond == 1_000 Microseconds
    [InlineData(DurationUnit.Millisecond, 1, DurationUnit.Microsecond, 1_000)]
    // Minute: 1 Minute == 60 Seconds
    [InlineData(DurationUnit.Minute, 1, DurationUnit.Second, 60)]
    // Weekend: 1 Weekend == 2 Days
    [InlineData(DurationUnit.Weekend, 1, DurationUnit.Day, 2)]
    // Decade: 1 Decade == 10 Years (Year == 360 days => Decade == 3600 days)
    [InlineData(DurationUnit.Decade, 1, DurationUnit.Year, 10)]
    // Cross-checks for the already-tested reference units, to keep the chain honest.
    [InlineData(DurationUnit.Second, 1, DurationUnit.Nanosecond, 1_000_000_000)]
    [InlineData(DurationUnit.Hour, 1, DurationUnit.Minute, 60)]
    [InlineData(DurationUnit.Day, 1, DurationUnit.Hour, 24)]
    [InlineData(DurationUnit.Week, 1, DurationUnit.Day, 7)]
    [InlineData(DurationUnit.Year, 1, DurationUnit.Day, 360)]
    [InlineData(DurationUnit.Century, 1, DurationUnit.Year, 100)]
    [InlineData(DurationUnit.Millennium, 1, DurationUnit.Year, 1_000)]
    public void ConversionFactor_IsExact(
        DurationUnit unit, long unitCount, DurationUnit reference, long referenceCount)
    {
        var subject = D(unitCount, unit);
        var exact = D(referenceCount, reference);
        var justUnder = D(referenceCount - 1, reference);
        var justOver = D(referenceCount + 1, reference);

        // Same absolute length: CompareTo ties on length, then breaks by unit ordinal only.
        // A wrong factor would make lengths differ, so byLength would dominate and this equality
        // would no longer be "0 unless units differ".
        Assert.Equal(
            unit.CompareTo(reference),
            Math.Sign(subject.CompareTo(exact)));

        // One reference-unit shorter must be strictly shorter than the subject.
        Assert.True(subject.CompareTo(justUnder) > 0,
            $"{unitCount} {unit} should be strictly longer than {referenceCount - 1} {reference}.");

        // One reference-unit longer must be strictly longer than the subject.
        Assert.True(subject.CompareTo(justOver) < 0,
            $"{unitCount} {unit} should be strictly shorter than {referenceCount + 1} {reference}.");
    }

    /// <summary>
    ///     Independent ordering anchors for the six previously-untested units, expressed against units
    ///     whose factors are pinned elsewhere. These catch factor mutations that happen to preserve the
    ///     boundary in <see cref="ConversionFactor_IsExact" /> for a single reference.
    /// </summary>
    [Fact]
    public void UntestedUnits_OrderCorrectlyRelativeToNeighbors()
    {
        // Tick (100 ns) sits strictly between 99 ns and 101 ns.
        Assert.True(D(1, DurationUnit.Tick) > D(99, DurationUnit.Nanosecond));
        Assert.True(D(1, DurationUnit.Tick) < D(101, DurationUnit.Nanosecond));

        // Microsecond (1_000 ns) is 10 Ticks exactly.
        Assert.Equal(
            DurationUnit.Microsecond.CompareTo(DurationUnit.Tick),
            Math.Sign(D(1, DurationUnit.Microsecond).CompareTo(D(10, DurationUnit.Tick))));
        Assert.True(D(1, DurationUnit.Microsecond) > D(9, DurationUnit.Tick));
        Assert.True(D(1, DurationUnit.Microsecond) < D(11, DurationUnit.Tick));

        // Millisecond (1_000_000 ns) is 1_000_000 Nanoseconds.
        Assert.True(D(1, DurationUnit.Millisecond) > D(999_999, DurationUnit.Nanosecond));
        Assert.True(D(1, DurationUnit.Millisecond) < D(1_000_001, DurationUnit.Nanosecond));

        // Minute (60 s) is shorter than an Hour (3600 s) and longer than 59 Seconds.
        Assert.True(D(1, DurationUnit.Minute) > D(59, DurationUnit.Second));
        Assert.True(D(60, DurationUnit.Minute).CompareTo(D(1, DurationUnit.Hour)) < 0); // 3600 s tie, Minute ordinal < Hour
        Assert.True(D(59, DurationUnit.Minute) < D(1, DurationUnit.Hour));
        Assert.True(D(61, DurationUnit.Minute) > D(1, DurationUnit.Hour));

        // Weekend (2 days) is strictly between 1 and 3 days, and shorter than a Week (7 days).
        Assert.True(D(1, DurationUnit.Weekend) > D(1, DurationUnit.Day));
        Assert.True(D(1, DurationUnit.Weekend) < D(3, DurationUnit.Day));
        Assert.True(D(1, DurationUnit.Weekend) < D(1, DurationUnit.Week));

        // Decade (3600 days) is 10 Years and one tenth of a Century (36000 days).
        Assert.True(D(1, DurationUnit.Decade) > D(3_599, DurationUnit.Day));
        Assert.True(D(1, DurationUnit.Decade) < D(3_601, DurationUnit.Day));
        Assert.True(D(10, DurationUnit.Decade).CompareTo(D(1, DurationUnit.Century)) < 0); // 36000 days tie, Decade ordinal < Century
        Assert.True(D(9, DurationUnit.Decade) < D(1, DurationUnit.Century));
        Assert.True(D(11, DurationUnit.Decade) > D(1, DurationUnit.Century));
    }
}
