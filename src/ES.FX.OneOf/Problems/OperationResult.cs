using ES.FX.Problems;
using JetBrains.Annotations;
using OneOf;

namespace ES.FX.OneOfExtensions.Problems;

[GenerateOneOf]
public partial class OperationResult<T> : OneOfBase<T, Problem>, IOneOfWithProblem
{
    [PublicAPI] public T AsResult => AsT0;

    [PublicAPI]
    public bool TryPickProblem(out Problem problem, out T result) => TryPickT1(out problem, out result);

    [PublicAPI]
    public bool TryPickResult(out T result) => TryPickT0(out result, out _);
}