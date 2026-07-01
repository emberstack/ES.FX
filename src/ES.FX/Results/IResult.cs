namespace ES.FX.Results;

/// <summary>
///     Represents an object result with a <see cref="Value" />
/// </summary>
public interface IResult
{
    /// <summary>
    ///     Gets the result's value.
    /// </summary>
    /// <remarks>
    ///     Because this returns <see cref="object" />, a value-type payload is boxed on every access. In hot paths prefer
    ///     the strongly typed accessors on <see cref="Result{T}" /> (<c>AsResult</c>, <c>TryPickResult</c>, or
    ///     <c>TryPickProblem</c>) which avoid the allocation.
    /// </remarks>
    object Value { get; }
}