using System.Numerics;
using JetBrains.Annotations;

namespace ES.FX.Primitives;

/// <summary>
///     Represents a quantity of time, in a specified unit.
/// </summary>
[PublicAPI]
public readonly record struct DurationValue : IComparable<DurationValue>, IComparable
{
    private static readonly BigInteger NanosecondsPerSecond = 1_000_000_000;
    private static readonly BigInteger NanosecondsPerDay = 86_400 * NanosecondsPerSecond;

    /// <summary>
    ///     Initializes a new <see cref="DurationValue" /> with the specified value and unit,
    ///     enforcing non-negative values.
    /// </summary>
    /// <param name="value">The number of units. Must be non-negative.</param>
    /// <param name="unit">The duration unit.</param>
    public DurationValue(long value, DurationUnit unit)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative");

        Value = value;
        Unit = unit;
    }

    /// <summary>
    ///     The number of units. Must be non-negative.
    /// </summary>
    public long Value
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative");

            field = value;
        }
    }

    /// <summary>
    ///     The unit of time for this duration.
    /// </summary>
    public DurationUnit Unit { get; init; }

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        DurationValue other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(DurationValue)}.", nameof(obj))
    };

    /// <summary>
    ///     Compares this duration with another, ordering by absolute length across units.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Durations are ordered by their absolute length. To provide a total order across every
    ///         <see cref="DurationUnit" /> (as required by <see cref="IComparable{T}" />), calendar units use
    ///         fixed conventions for ordering purposes only: a month is 30 days, a quarter 90 days, a year
    ///         360 days, and decade/century/millennium are multiples of the 360-day year (so 12 months == 1
    ///         year and 4 quarters == 1 year).
    ///     </para>
    ///     <para>
    ///         When two durations have the same absolute length but different units (for example 60 seconds
    ///         and 1 minute), ties are broken by <see cref="Unit" />. As a result the comparison is consistent
    ///         with equality: <see cref="CompareTo(DurationValue)" /> returns 0 only when both
    ///         <see cref="Value" /> and <see cref="Unit" /> are equal.
    ///     </para>
    /// </remarks>
    public int CompareTo(DurationValue other)
    {
        var byLength = ToNanoseconds().CompareTo(other.ToNanoseconds());
        return byLength != 0 ? byLength : Unit.CompareTo(other.Unit);
    }

    private BigInteger ToNanoseconds() => Value * NanosecondsPer(Unit);

    private static BigInteger NanosecondsPer(DurationUnit unit) => unit switch
    {
        DurationUnit.Nanosecond => BigInteger.One,
        DurationUnit.Tick => 100,
        DurationUnit.Microsecond => 1_000,
        DurationUnit.Millisecond => 1_000_000,
        DurationUnit.Second => NanosecondsPerSecond,
        DurationUnit.Minute => 60 * NanosecondsPerSecond,
        DurationUnit.Hour => 3_600 * NanosecondsPerSecond,
        DurationUnit.Day => NanosecondsPerDay,
        DurationUnit.Weekend => 2 * NanosecondsPerDay,
        DurationUnit.Week => 7 * NanosecondsPerDay,
        DurationUnit.Month => 30 * NanosecondsPerDay,
        DurationUnit.Quarter => 90 * NanosecondsPerDay,
        DurationUnit.Year => 360 * NanosecondsPerDay,
        DurationUnit.Decade => 3_600 * NanosecondsPerDay,
        DurationUnit.Century => 36_000 * NanosecondsPerDay,
        DurationUnit.Millennium => 360_000 * NanosecondsPerDay,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
    };

    /// <summary>
    ///     Returns a string such as "7 Days".
    /// </summary>
    public override string ToString() => Value == 1
        ? $"{Value} {Unit}"
        : $"{Value} {Unit switch
        {
            DurationUnit.Century => "Centuries",
            DurationUnit.Millennium => "Millennia",
            _ => $"{Unit}s"
        }}";

    public static bool operator >(DurationValue left, DurationValue right) =>
        left.CompareTo(right) > 0;

    public static bool operator <(DurationValue left, DurationValue right) =>
        left.CompareTo(right) < 0;

    public static bool operator >=(DurationValue left, DurationValue right) =>
        left.CompareTo(right) >= 0;

    public static bool operator <=(DurationValue left, DurationValue right) =>
        left.CompareTo(right) <= 0;
}