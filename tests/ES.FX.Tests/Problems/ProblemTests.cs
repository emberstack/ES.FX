using ES.FX.Problems;

namespace ES.FX.Tests.Problems;

public class ProblemTests
{
    [Fact]
    public void Problem_Default_TypeIsAboutBlank()
    {
        var problem = new Problem();
        Assert.Equal("about:blank", problem.Type);
        Assert.Null(problem.Title);
        Assert.Null(problem.Detail);
        Assert.Null(problem.Instance);
        Assert.Null(problem.Status);
    }

    [Fact]
    public void Problem_Parameterized_MapsAllValues()
    {
        var problem = new Problem(
            "https://example.com/type",
            "A title",
            "A detail",
            "https://example.com/instance",
            418);

        Assert.Equal("https://example.com/type", problem.Type);
        Assert.Equal("A title", problem.Title);
        Assert.Equal("A detail", problem.Detail);
        Assert.Equal("https://example.com/instance", problem.Instance);
        Assert.Equal(418, problem.Status);
    }

    [Fact]
    public void Problem_Parameterized_DefaultsTypeToAboutBlank()
    {
        var problem = new Problem(title: "just a title");
        Assert.Equal("about:blank", problem.Type);
        Assert.Equal("just a title", problem.Title);
    }

    [Fact]
    public void Problem_RecordEquality_ByValue()
    {
        Assert.Equal(new Problem(title: "x"), new Problem(title: "x"));
        Assert.NotEqual(new Problem(title: "x"), new Problem(title: "y"));
    }

    [Fact]
    public void ValidationProblem_Default_TypeAndTitleSet()
    {
        var problem = new ValidationProblem();
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.NotNull(problem.Errors);
        Assert.Empty(problem.Errors);
    }

    [Fact]
    public void ValidationProblem_WithErrors_ExposesErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Required", "Too short" },
            ["Age"] = new[] { "Must be positive" }
        };

        var problem = new ValidationProblem(errors);

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", problem.Type);
        Assert.Same(errors, problem.Errors);
        Assert.Equal(new[] { "Required", "Too short" }, problem.Errors["Name"]);
    }

    [Fact]
    public void ProblemException_Message_ComposesTypeAndProblem()
    {
        var problem = new Problem("my:type", "Boom");
        var exception = new ProblemException(problem);

        Assert.Contains("my:type", exception.Message);
        Assert.Same(problem, exception.Problem);
    }

    [Fact]
    public void ProblemExtensions_Throw_ThrowsProblemException_WithProblem()
    {
        var problem = new Problem(title: "kaboom");
        var exception = Assert.Throws<ProblemException>(() => problem.Throw());
        Assert.Same(problem, exception.Problem);
    }
}
