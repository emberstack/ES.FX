using JetBrains.Annotations;

namespace ES.FX.Primitives;

[PublicAPI]
public readonly record struct ValueRange<T> : IComparable<ValueRange<T>>, IComparable
    where T : IComparable<T>
{
    /// <summary>
    ///     Creates a range with the same min and max values
    /// </summary>
    /// <param name="exact"></param>
    public ValueRange(T exact)
    {
        Min = exact;
        Max = exact;
    }

    /// <summary>
    ///     Creates a range with the specified min and max values
    /// </summary>
    /// <param name="min">The <see cref="Min" /> value</param>
    /// <param name="max">The <see cref="Max" /> value</param>
    /// <exception cref="ArgumentException">
    ///     Throws if the <see cref="Min" /> value is greater than the <see cref="Max" />
    ///     value.
    /// </exception>
    public ValueRange(T min, T max)
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException($"{nameof(Min)} cannot be greater than {nameof(Max)}.");

        Min = min;
        Max = max;
    }

    /// <summary>
    ///     Creates a range with the same min and max values as the specified range
    /// </summary>
    /// <param name="range"></param>
    public ValueRange(ValueRange<T> range) : this(range.Min, range.Max)
    {
    }

    /// <summary>
    ///     The minimum value of the range
    /// </summary>
    /// <remarks>
    ///     Declared <c>init</c> so configuration binders and serializers &#8212; which construct via the
    ///     parameterless <c>struct</c> constructor and then assign members &#8212; can populate the range.
    ///     The <see cref="Min" /> &lt;= <see cref="Max" /> invariant is enforced by the value constructors;
    ///     it is <b>not</b> re-checked for object initializers, <c>with</c> expressions, or bound
    ///     configuration, because a <c>struct</c> can always be produced as <c>default</c> and bypass any
    ///     constructor regardless.
    /// </remarks>
    public T Min { get; init; }

    /// <summary>
    ///     The maximum value of the range
    /// </summary>
    /// <remarks>
    ///     Declared <c>init</c> so configuration binders and serializers &#8212; which construct via the
    ///     parameterless <c>struct</c> constructor and then assign members &#8212; can populate the range.
    ///     The <see cref="Min" /> &lt;= <see cref="Max" /> invariant is enforced by the value constructors;
    ///     it is <b>not</b> re-checked for object initializers, <c>with</c> expressions, or bound
    ///     configuration, because a <c>struct</c> can always be produced as <c>default</c> and bypass any
    ///     constructor regardless.
    /// </remarks>
    public T Max { get; init; }

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        ValueRange<T> other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(ValueRange<T>)}.", nameof(obj))
    };

    /// <summary>
    ///     Compares the current range with another range, ordering by <see cref="Min" /> then <see cref="Max" />.
    /// </summary>
    public int CompareTo(ValueRange<T> other)
    {
        var minComparison = Min.CompareTo(other.Min);
        return minComparison != 0 ? minComparison : Max.CompareTo(other.Max);
    }

    /// <summary>
    ///     Gets if the range contains the specified value
    /// </summary>
    /// <param name="value">The value to seek in the range</param>
    /// <returns>True if the value is contained. False if the value is outside the range.</returns>
    public bool Contains(T value) => value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;

    public ValueRange<T>? Intersect(ValueRange<T> other)
    {
        var newMin = Min.CompareTo(other.Min) > 0 ? Min : other.Min;
        var newMax = Max.CompareTo(other.Max) < 0 ? Max : other.Max;
        return newMin.CompareTo(newMax) <= 0 ? new ValueRange<T>(newMin, newMax) : null;
    }

    /// <summary>
    ///     Gets if the range is exact (<see cref="Min" />==<see cref="Max" />)
    /// </summary>
    public bool IsExact() => Min.CompareTo(Max) == 0;

    /// <inheritdoc />
    public override string ToString() => $"[{Min}, {Max}]";
}