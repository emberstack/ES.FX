using ES.FX.Problems;
using ES.FX.Results;

namespace ES.FX.Tests.Results;

public class ResultAccessorsTests
{
    // ---- TryPickResult (single out) ----

    [Fact]
    public void TryPickResult_Single_WhenResult_ReturnsTrue_AndValue()
    {
        Result<string> result = "value";
        Assert.True(result.TryPickResult(out var picked));
        Assert.Equal("value", picked);
    }

    [Fact]
    public void TryPickResult_Single_WhenProblem_ReturnsFalse_AndNull()
    {
        Result<string> result = new Problem();
        Assert.False(result.TryPickResult(out var picked));
        Assert.Null(picked);
    }

    // ---- TryPickResult (two out) ----

    [Fact]
    public void TryPickResult_Both_WhenResult_ResultSet_ProblemNull()
    {
        Result<string> result = "value";
        Assert.True(result.TryPickResult(out var picked, out var problem));
        Assert.Equal("value", picked);
        Assert.Null(problem);
    }

    [Fact]
    public void TryPickResult_Both_WhenProblem_ResultNull_ProblemSet()
    {
        var theProblem = new Problem(title: "boom");
        Result<string> result = theProblem;
        Assert.False(result.TryPickResult(out var picked, out var problem));
        Assert.Null(picked);
        Assert.Same(theProblem, problem);
    }

    // ---- TryPickProblem (single out) ----

    [Fact]
    public void TryPickProblem_Single_WhenProblem_ReturnsTrue_AndProblem()
    {
        var theProblem = new Problem(title: "boom");
        Result<string> result = theProblem;
        Assert.True(result.TryPickProblem(out var problem));
        Assert.Same(theProblem, problem);
    }

    [Fact]
    public void TryPickProblem_Single_WhenResult_ReturnsFalse_AndNull()
    {
        Result<string> result = "value";
        Assert.False(result.TryPickProblem(out var problem));
        Assert.Null(problem);
    }

    // ---- TryPickProblem (two out) ----

    [Fact]
    public void TryPickProblem_Both_WhenProblem_ProblemSet_ResultNull()
    {
        var theProblem = new Problem(title: "boom");
        Result<string> result = theProblem;
        Assert.True(result.TryPickProblem(out var problem, out var picked));
        Assert.Same(theProblem, problem);
        Assert.Null(picked);
    }

    [Fact]
    public void TryPickProblem_Both_WhenResult_ProblemNull_ResultSet()
    {
        Result<string> result = "value";
        Assert.False(result.TryPickProblem(out var problem, out var picked));
        Assert.Null(problem);
        Assert.Equal("value", picked);
    }

    // ---- AsResult / AsProblem throwing paths ----

    [Fact]
    public void AsResult_WhenResult_ReturnsValue()
    {
        Result<string> result = "value";
        Assert.Equal("value", result.AsResult);
    }

    [Fact]
    public void AsResult_WhenProblem_Throws()
    {
        Result<string> result = new Problem();
        Assert.Throws<InvalidOperationException>(() => result.AsResult);
    }

    [Fact]
    public void AsProblem_WhenProblem_ReturnsProblem()
    {
        var theProblem = new Problem(title: "boom");
        Result<string> result = theProblem;
        Assert.Same(theProblem, result.AsProblem);
    }

    [Fact]
    public void AsProblem_WhenResult_Throws()
    {
        Result<string> result = "value";
        Assert.Throws<InvalidOperationException>(() => result.AsProblem);
    }

    // ---- Explicit operators ----

    [Fact]
    public void ExplicitOperator_ToT_WhenResult_ReturnsValue()
    {
        Result<string> result = "value";
        var value = (string)result;
        Assert.Equal("value", value);
    }

    [Fact]
    public void ExplicitOperator_ToT_WhenProblem_Throws()
    {
        Result<string> result = new Problem();
        Assert.Throws<InvalidOperationException>(() => (string)result);
    }

    [Fact]
    public void ExplicitOperator_ToProblem_WhenProblem_ReturnsProblem()
    {
        var theProblem = new Problem(title: "boom");
        Result<string> result = theProblem;
        var problem = (Problem)result;
        Assert.Same(theProblem, problem);
    }

    [Fact]
    public void ExplicitOperator_ToProblem_WhenResult_Throws()
    {
        Result<string> result = "value";
        Assert.Throws<InvalidOperationException>(() => (Problem)result);
    }

    // ---- Non-generic Result explicit operator ----

    [Fact]
    public void Result_ExplicitOperator_ToProblem_WhenResult_Throws()
    {
        var result = Result.Success;
        Assert.Throws<InvalidOperationException>(() => (Problem)result);
    }

    [Fact]
    public void Result_ExplicitOperator_ToProblem_WhenProblem_ReturnsProblem()
    {
        var theProblem = new Problem(title: "boom");
        Result result = theProblem;
        Assert.Same(theProblem, (Problem)result);
    }
}
