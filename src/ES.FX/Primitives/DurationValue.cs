using JetBrains.Annotations;

namespace ES.FX.Primitives;

/// <summary>
///   Represents a quantity of time, in a specified unit.
/// </summary>
[PublicAPI]
public readonly record struct DurationValue : IComparable<DurationValue>
{
    /// <summary>
    ///   The number of units.
    /// </summary>
    public long Count { get; init; }

    /// <summary>
    ///   The unit of time for this duration.
    /// </summary>
    public DurationUnit Unit { get; init; }

    /// <summary>
    ///   Initializes a new <see cref="DurationValue"/> with the specified count and unit,
    ///   enforcing non-negative counts.
    /// </summary>
    /// <param name="count">The number of units. Must be non-negative.</param>
    /// <param name="unit">The duration unit.</param>
    public DurationValue(long count, DurationUnit unit)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative");

        Count = count;
        Unit = unit;
    }

    /// <summary>
    ///   Returns a string such as "7 Days".
    /// </summary>
    public override string ToString() => $"{Count} {Unit}{(Count == 1 ? string.Empty : "s")}";

    /// <summary>
    ///   Compares two durations of the same unit.
    ///   Comparing different units is not supported.
    /// </summary>
    public int CompareTo(DurationValue other)
    {
        if (Unit != other.Unit)
            throw new InvalidOperationException("Cannot compare durations of different units.");

        return Count.CompareTo(other.Count);
    }
}