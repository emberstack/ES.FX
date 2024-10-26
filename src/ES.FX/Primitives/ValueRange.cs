using JetBrains.Annotations;

namespace ES.FX.Primitives;

[PublicAPI]
public readonly record struct ValueRange<T> where T : IComparable<T>
{
    public ValueRange(T exact)
    {
        Min = exact;
        Max = exact;
    }
    public ValueRange(T min, T max)
    {
        if (min.CompareTo(max) > 0)
            throw new ArgumentException($"{nameof(Min)} cannot be greater than {nameof(Max)}.");

        Min = min;
        Max = max;
    }

    public T Min { get; init; }
    public T Max { get; init; }

    public bool IsExact => Min.CompareTo(Max) == 0;

    public bool IsInRange(T value) => value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;

    public ValueRange<T>? Intersect(ValueRange<T> other)
    {
        var newMin = Min.CompareTo(other.Min) > 0 ? Min : other.Min;
        var newMax = Max.CompareTo(other.Max) < 0 ? Max : other.Max;
        return newMin.CompareTo(newMax) <= 0 ? new ValueRange<T>(newMin, newMax) : null;
    }

    public override string ToString() => $"[{Min}, {Max}]";

    public int CompareTo(ValueRange<T>? other)
    {
        if (other is null) return 1;

        var minComparison = Min.CompareTo(other.Value.Min);
        return minComparison != 0 ? minComparison : Max.CompareTo(other.Value.Max);
    }
}