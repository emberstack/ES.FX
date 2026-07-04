using ES.FX.Additions.OneOf.Problems;
using ES.FX.Additions.OneOf.Types;
using ES.FX.Problems;
using OneOfBridge.Tests.Unions;

namespace ES.FX.Additions.OneOf.Tests;

/// <summary>
///     Functional regression coverage for the Problem bridge:
///     <see cref="IOneOfWithProblem" /> + <see cref="OneOfProblemExtensions.TryPickProblem" />.
/// </summary>
public class ProblemBridgeTests
{
    [Fact]
    public void TryPickProblem_WhenUnionHoldsProblem_ReturnsTrueAndProblem()
    {
        var problem = new Problem(title: "Nope", status: 400);
        ResultOrProblem union = problem;

        var picked = union.TryPickProblem(out var extracted);

        Assert.True(picked);
        Assert.NotNull(extracted);
        Assert.Same(problem, extracted);
        Assert.Equal("Nope", extracted.Title);
        Assert.Equal(400, extracted.Status);
    }

    [Fact]
    public void TryPickProblem_WhenUnionHoldsFirstSlotValue_ReturnsFalseAndNull()
    {
        ResultOrProblem union = "all good";

        var picked = union.TryPickProblem(out var extracted);

        Assert.False(picked);
        Assert.Null(extracted);
    }

    [Fact]
    public void TryPickProblem_WhenUnionHoldsLastSlotValue_ReturnsFalseAndNull()
    {
        ResultOrProblem union = 123;

        var picked = union.TryPickProblem(out var extracted);

        Assert.False(picked);
        Assert.Null(extracted);
    }

    [Fact]
    public void TryPickProblem_IsSlotAgnostic_FindsProblemRegardlessOfPosition()
    {
        // Problem sits in slot index 1 (the middle), proving extraction is not slot 0-only.
        var problem = new Problem(detail: "middle slot");
        ResultOrProblem union = problem;

        Assert.True(union.TryPickProblem(out var extracted));
        Assert.Equal("middle slot", extracted.Detail);
    }

    [Fact]
    public void TryPickProblem_OnUnionThatNeverHoldsProblem_ReturnsFalse()
    {
        // The union type declares IOneOfWithProblem but its current value is a Fault, not a Problem.
        ValueOrFault union = new Fault();

        var picked = union.TryPickProblem(out var extracted);

        Assert.False(picked);
        Assert.Null(extracted);
    }

    [Fact]
    public void TryPickProblem_MatchesOnRuntimeType_DerivedProblemStillMatches()
    {
        // Problem is a record with subtypes possible; ensure an 'is Problem' check honors inheritance.
        var derived = new ValidationProblem("Invalid");
        ResultOrProblem union = derived;

        Assert.True(union.TryPickProblem(out var extracted));
        Assert.Same(derived, extracted);
    }

    [Fact]
    public void TryPickProblem_WithNullUnion_ThrowsArgumentNullException()
    {
        IOneOfWithProblem nullUnion = null!;

        var ex = Assert.Throws<ArgumentNullException>(() => nullUnion.TryPickProblem(out _));
        Assert.Equal("oneOf", ex.ParamName);
    }

    /// <summary>A <see cref="Problem" /> subtype to exercise runtime-type matching in the bridge.</summary>
    private sealed record ValidationProblem : Problem
    {
        public ValidationProblem(string? title = null) : base(title: title)
        {
        }
    }
}