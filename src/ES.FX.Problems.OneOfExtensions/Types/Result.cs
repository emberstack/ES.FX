using JetBrains.Annotations;
using OneOf;

namespace ES.FX.Problems.OneOfExtensions.Types;

[GenerateOneOf]
public partial class Result<T> : OneOfBase<T, Problem>, IOneOfWithProblem
{
    [PublicAPI] public T AsResult => AsT0;

    [PublicAPI]
    public bool TryPickResult(out T result) => TryPickT0(out result, out _);

    [PublicAPI]
    public bool TryPickProblem(out Problem problem, out T result) => TryPickT1(out problem, out result);
}