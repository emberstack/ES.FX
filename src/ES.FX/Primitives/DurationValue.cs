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
    public long Value { get; init; }

    /// <summary>
    ///   The unit of time for this duration.
    /// </summary>
    public DurationUnit Unit { get; init; }

    /// <summary>
    ///   Initializes a new <see cref="DurationValue"/> with the specified value and unit,
    ///   enforcing non-negative values.
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
    ///   Returns a string such as "7 Days".
    /// </summary>
    public override string ToString() => $"{Value} {Unit}{(Value == 1 ? string.Empty : "s")}";

    /// <summary>
    ///   Compares two durations of the same unit.
    ///   Comparing different units is not supported.
    /// </summary>
    public int CompareTo(DurationValue other)
    {
        if (Unit != other.Unit)
            throw new InvalidOperationException("Cannot compare durations of different units.");

        return Value.CompareTo(other.Value);
    }
}