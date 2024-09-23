namespace ES.FX.ValueRanges;

public abstract record ValueRange<T> where T : IComparable<T>
{
    protected ValueRange(T min, T max)
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException("Min cannot be greater than Max.");

        Min = min;
        Max = max;
    }

    public T Min { get; init; }
    public T Max { get; init; }

    public bool IsInRange(T value) => value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;

    public ValueRange<T>? Intersect(ValueRange<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var newMin = Min.CompareTo(other.Min) > 0 ? Min : other.Min;
        var newMax = Max.CompareTo(other.Max) < 0 ? Max : other.Max;
        return newMin.CompareTo(newMax) <= 0 ? CreateFor(newMin, newMax) : null;
    }

    protected abstract ValueRange<T> CreateFor(T min, T max);

    public override string ToString() => $"[{Min}, {Max}]";

    public int CompareTo(ValueRange<T>? other)
    {
        if (other is null) return 1;

        var minComparison = Min.CompareTo(other.Min);
        return minComparison != 0 ? minComparison : Max.CompareTo(other.Max);
    }
}