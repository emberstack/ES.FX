using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Primitives;

/// <summary>
///     Represents an optional value that may or may not be present.
/// </summary>
/// <typeparam name="T">The underlying type of the optional value.</typeparam>
[PublicAPI]
public readonly struct Optional<T>
{
    private readonly T? _value;

    /// <summary>
    ///     Gets a value indicating whether the optional contains a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    ///     Gets the value contained in the optional. This value may be <c>null</c>.
    ///     Throws <see cref="InvalidOperationException" /> if <see cref="HasValue" /> is <c>false</c>.
    /// </summary>
    public T? Value
    {
        get
        {
            if (!HasValue)
                throw new InvalidOperationException("No value present.");
            return _value;
        }
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Optional{T}" /> struct.
    /// </summary>
    /// <param name="value">The value to wrap. May be <c>null</c>.</param>
    /// <param name="hasValue">Indicates whether the value is present.</param>
    [JsonConstructor]
    public Optional(T? value, bool hasValue)
    {
        _value = value;
        HasValue = hasValue;
    }

    /// <summary>
    ///     Creates a new <see cref="Optional{T}" /> with a present value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An <see cref="Optional{T}" /> containing the specified value.</returns>
    public static Optional<T> From(T value) => new(value, true);

    /// <summary>
    ///     Creates an <see cref="Optional{T}" /> with no value present.
    /// </summary>
    /// <returns>An <see cref="Optional{T}" /> representing the absence of a value.</returns>
    public static Optional<T> None() => new(default, false);

    /// <summary>
    ///     Gets the value if present; otherwise returns the default value of <typeparamref name="T" />.
    ///     Does not throw an exception.
    /// </summary>
    public T? GetValueOrDefault() => HasValue ? _value : default;

    /// <summary>
    ///     Gets the value if present; otherwise returns the specified fallback value.
    /// </summary>
    /// <param name="defaultValue">The fallback value to return if the optional is empty.</param>
    /// <returns>The value if present; otherwise <paramref name="defaultValue" />.</returns>
    public T GetValueOrDefault(T defaultValue) => HasValue ? _value! : defaultValue;

    /// <summary>
    ///     Matches on the presence or absence of a value and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="whenSome">A function to invoke if the value is present (even if <c>null</c>).</param>
    /// <param name="whenNone">A function to invoke if the value is not present.</param>
    /// <returns>
    ///     The result of either <paramref name="whenSome" /> or <paramref name="whenNone" /> depending on the presence of
    ///     a value.
    /// </returns>
    public TResult Match<TResult>(Func<T?, TResult> whenSome, Func<TResult> whenNone) =>
        HasValue ? whenSome(Value) : whenNone();

    /// <summary>
    ///     Attempts to get the value if present.
    /// </summary>
    /// <param name="value">
    ///     When this method returns, contains the value if present; otherwise, the default value for the type
    ///     of the value parameter.
    /// </param>
    /// <returns><c>true</c> if the optional has a value; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(out T? value)
    {
        value = HasValue ? _value : default;
        return HasValue;
    }
}