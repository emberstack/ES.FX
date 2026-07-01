using OneOf;

namespace ES.FX.Additions.OneOf.Problems;

/// <summary>
///     Marker interface for a <see cref="IOneOf" /> union whose cases include an <c>ES.FX.Problems.Problem</c>.
///     Enables slot-agnostic extraction via
///     <see cref="OneOfProblemExtensions.TryPickProblem" />.
/// </summary>
public interface IOneOfWithProblem : IOneOf;