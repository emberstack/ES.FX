using System.Diagnostics.CodeAnalysis;
using ES.FX.Problems;
using JetBrains.Annotations;

namespace ES.FX.Results;

/// <summary>
///     Result that can be either a <see cref="Success" /> or a <see cref="Problem" />
/// </summary>
[PublicAPI]
public class Result : Result<bool>
{
    public Result() : base(true)
    {
    }

    public Result(Problem problem) : base(problem)
    {
    }

    /// <summary>
    ///     Returns a <see cref="Result{T}" /> that is successful
    /// </summary>
    public static Result Success => new();


    // Conversion from Problem to Result and back
    public static implicit operator Result(Problem _) => new(_);
    public static explicit operator Problem(Result _) => _.AsProblem;
}

/// <summary>
///     Result that can either a <typeparamref name="T" /> or a <see cref="Problem" />
/// </summary>
/// <typeparam name="T">Type of result</typeparam>
[PublicAPI]
public class Result<T> : IResult where T : notnull
{
    private readonly Problem? _problem;
    private readonly T? _result;

    /// <summary>
    ///     Creates a new <see cref="Result{T}" /> with the <see cref="Value" /> set to the <typeparamref name="T" /> value
    /// </summary>
    /// <param name="value">The value for the result</param>
    public Result(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _result = value;
        IsResult = true;
    }

    /// <summary>
    ///     Creates a new <see cref="Result{T}" /> with the <see cref="Value" /> set to the <see cref="Problem" /> value
    /// </summary>
    /// <param name="problem">The problem for the result</param>
    public Result(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        _problem = problem;
        IsResult = false;
    }

    /// <summary>
    ///     Gets a boolean indicating if the <see cref="Value" />> is a <typeparamref name="T" />
    /// </summary>
    [MemberNotNullWhen(true, nameof(_result))]
    [MemberNotNullWhen(false, nameof(_problem))]
    public bool IsResult { get; }

    /// <summary>
    ///     Gets a boolean indicating if the <see cref="Value" />> is a <see cref="Problem" />
    /// </summary>
    [MemberNotNullWhen(true, nameof(_problem))]
    [MemberNotNullWhen(false, nameof(_result))]
    public bool IsProblem => !IsResult;

    /// <summary>
    ///     Gets the <see cref="Result{T}" /> as a <typeparamref name="T" />
    /// </summary>
    public T AsResult => IsResult
        ? _result ?? throw new NullReferenceException()
        : throw new InvalidOperationException("Cannot return as result.");

    /// <summary>
    ///     Gets the <see cref="Result{T}" /> as a <see cref="Problem" />
    /// </summary>
    public Problem AsProblem => IsProblem
        ? _problem ?? throw new NullReferenceException()
        : throw new InvalidOperationException("Cannot return as problem.");

    /// <summary>
    ///     Gets the result's value. Can be either a <typeparamref name="T" /> or <see cref="Problem" />
    /// </summary>
    /// <remarks>
    ///     Because this returns <see cref="object" />, accessing a value-type <typeparamref name="T" /> through this
    ///     property boxes the payload on every call. In hot paths prefer the strongly typed accessors
    ///     (<see cref="AsResult" />, <see cref="TryPickResult(out T)" />, or <see cref="TryPickProblem(out Problem)" />)
    ///     which avoid the allocation.
    /// </remarks>
    public object Value => IsResult ? _result : _problem;

    /// <summary>
    ///     Returns true if the <see cref="Result{T}" /> can be returned as a <typeparamref name="T" />
    /// </summary>
    public bool TryPickResult([NotNullWhen(true)] out T? result) => TryPickResult(out result, out _);


    /// <summary>
    ///     Returns true if the <see cref="Result{T}" /> can be returned as a <typeparamref name="T" />
    /// </summary>
    public bool TryPickResult([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out Problem? problem)
    {
        result = IsResult ? _result : default;
        problem = IsProblem ? _problem : null;
        return IsResult;
    }

    /// <summary>
    ///     Returns true if the <see cref="Result{T}" /> can be returned as a <see cref="Problem" />
    /// </summary>
    public bool TryPickProblem([NotNullWhen(true)] out Problem? problem) => TryPickProblem(out problem, out _);


    /// <summary>
    ///     Returns true if the <see cref="Result{T}" /> can be returned as a <see cref="Problem" />
    /// </summary>
    public bool TryPickProblem([NotNullWhen(true)] out Problem? problem, [NotNullWhen(false)] out T? result)
    {
        result = IsResult ? _result : default;
        problem = IsProblem ? _problem : null;
        return IsProblem;
    }


    public static implicit operator Result<T>(T _) => new(_);
    public static explicit operator T(Result<T> _) => _.AsResult;

    public static implicit operator Result<T>(Problem _) => new(_);
    public static explicit operator Problem(Result<T> _) => _.AsProblem;


    /// <summary>
    ///     Determines whether the specified object is equal to this result.
    /// </summary>
    /// <remarks>
    ///     Two <see cref="Result{T}" /> instances are equal when they share the same state (success or problem)
    ///     and their inner values are equal; a success (or problem) result also compares equal to a bare
    ///     <typeparamref name="T" /> (or <see cref="Problem" />) whose value is equal.
    ///     <para>
    ///         This cross-type equality is intentionally <b>asymmetric</b>: <c>result.Equals(value)</c> can be
    ///         <see langword="true" /> while <c>value.Equals(result)</c> is <see langword="false" />, and the two
    ///         produce different hash codes. Do not rely on order-independent equality when mixing
    ///         <see cref="Result{T}" /> and bare <typeparamref name="T" />/<see cref="Problem" /> values in the
    ///         same hash-based collection.
    ///     </para>
    /// </remarks>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        // Compare with another Result<T>
        if (obj is Result<T> otherResult)
        {
            if (IsResult != otherResult.IsResult)
                return false;
            return IsResult
                ? EqualityComparer<T>.Default.Equals(_result!, otherResult._result!)
                : EqualityComparer<Problem>.Default.Equals(_problem!, otherResult._problem!);
        }
        // Compare with a raw T if this is a success.

        if (IsResult && obj is T value) return EqualityComparer<T>.Default.Equals(_result!, value);
        // Compare with a Problem if this is a problem.
        if (IsProblem && obj is Problem prob) return EqualityComparer<Problem>.Default.Equals(_problem!, prob);
        return false;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + IsResult.GetHashCode();
            if (IsResult)
                hash = hash * 23 + (_result is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_result!));
            else
                hash = hash * 23 + (_problem is null ? 0 : _problem!.GetHashCode());
            return hash;
        }
    }

    // Overload the equality operators so that the "==" operator uses our Equals override.
    public static bool operator ==(Result<T>? left, object? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(Result<T>? left, object? right) => !(left == right);
}