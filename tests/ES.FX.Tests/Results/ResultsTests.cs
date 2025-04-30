using ES.FX.Problems;
using ES.FX.Results;

namespace ES.FX.Tests.Results;

public class ResultsTests
{

    [Fact]
    public void TypedResult_CanBe_Null()
    {
        var result = TypedResultAsNull();
        Assert.True(result.IsResult);

        if(result.TryPickResult(out var resultValue))
        {
            Assert.Null(resultValue);
        }

        if (!result.TryPickProblem(out var problemValue))
        {
            Assert.Null(problemValue);
        }
    }

    [Fact]
    public void Result_CanBe_Success()
    {
        var result = ResultAsSuccess();
        Assert.True(result.IsResult);
        Assert.False(result.IsProblem);
        Assert.IsType<bool>(result.Value);
    }

    [Fact]
    public void Result_CanBe_Problem()
    {
        var result = ResultAsProblem();
        Assert.False(result.IsResult);
        Assert.True(result.IsProblem);
        Assert.IsType<Problem>(result.Value);
    }

    [Fact]
    public void TypedResult_CanBe_Problem()
    {
        var result = TypedResultAsProblem();
        Assert.False(result.IsResult);
        Assert.True(result.IsProblem);
        Assert.IsType<Problem>(result.Value);
    }

    [Fact]
    public void TypedResult_CanBe_Success()
    {
        var result = TypedResultAsValue();
        Assert.True(result.IsResult);
        Assert.False(result.IsProblem);
        Assert.IsType<string>(result.Value);
    }

    [Fact]
    public void BooleanTypedResult_CanBe_Problem()
    {
        var result = BooleanResultAsProblem();
        Assert.False(result.IsResult);
        Assert.True(result.IsProblem);
        Assert.IsType<Problem>(result.Value);
    }

    [Fact]
    public void BooleanTypedResult_CanBe_True()
    {
        var result = BooleanResultAsTrue();
        Assert.True(result.IsResult);
        Assert.False(result.IsProblem);
        Assert.IsType<bool>(result.Value);
    }

    [Fact]
    public void BooleanTypedResult_CanBe_False()
    {
        var result = BooleanResultAsFalse();
        Assert.True(result.IsResult);
        Assert.False(result.IsProblem);
        Assert.IsType<bool>(result.Value);

        if (result.AsResult) Assert.Fail("Result value was evaluated as true");
    }


    [Fact]
    public void Results_CanBeEqualityChecked()
    {
        Assert.Equal(Result.Success, Result.Success);
        Assert.NotEqual(Result.Success, new Result(new Problem()));
        Assert.Equal(new Result(new Problem()), new Result(new Problem()));
        Assert.NotEqual(new Result(new Problem(Title: string.Empty)), new Result(new Problem(Title: "something")));

        Assert.Equal(new Result<bool>(true), new Result<bool>(true));
        Assert.NotEqual(new Result<bool>(true), new Result<bool>(false));

        Assert.Equal(new Result<string>(string.Empty), new Result<string>(string.Empty));

        var obj1 = new object();
        Assert.Equal(new Result<object>(obj1), new Result<object>(obj1));
        Assert.True(new Result<object>(obj1) == obj1);
        Assert.True(new Result<object>(obj1).Equals(obj1));

        Assert.NotEqual(new Result<object>(obj1), new Result<object>(new object()));
        Assert.False(new Result<object>(obj1) == new object());
        Assert.False(new Result<object>(obj1).Equals(new object()));
    }


    private static Result ResultAsSuccess() => Result.Success;
    private static Result ResultAsProblem() => new Problem();
    private static Result<bool> BooleanResultAsProblem() => new Problem();
    private static Result<bool> BooleanResultAsTrue() => true;
    private static Result<bool> BooleanResultAsFalse() => false;

    private static Result<string> TypedResultAsProblem() => new Problem();
    private static Result<string> TypedResultAsValue() => string.Empty;
    private static Result<object?> TypedResultAsNull() => (object?)null;


}