using ES.FX.Exceptions;

namespace ES.FX.Tests.Exceptions;

public class ExceptionExtensionsTests
{
    [Fact]
    public void Exception_InnerMost_ReturnsInnermostException()
    {
        var exception = new Exception("Outer", new Exception("Inner", new Exception("Innermost")));
        var result = exception.InnermostException();
        Assert.Equal("Innermost", result.Message);
    }

    [Fact]
    public void Exception_InnerMost_ReturnsNullForANullException()
    {
        Exception? exception = null;
        var result = exception.InnermostException<ArgumentException>();
        Assert.Null(result);
    }

    [Fact]
    public void Exception_InnerMost_ReturnsSelfIfNoInnerException()
    {
        var exception = new Exception("Outer");
        var result = exception.InnermostException();
        Assert.Equal("Outer", result.Message);
    }

    [Fact]
    public void Exception_InnerMostOfType_ReturnsInnermostExceptionOfType()
    {
        var exception = new Exception("Outer",
            new ArgumentException("Inner",
                new ArgumentException("Innermost",
                    new Exception("Last"))));
        var result = exception.InnermostException<ArgumentException>();
        Assert.NotNull(result);
        Assert.Equal("Innermost", result.Message);
    }

    [Fact]
    public void Exception_InnerMostOfType_ReturnsNullIfNoInnermostException()
    {
        var exception = new Exception("Outer",
            new ArgumentException("Inner",
                new ArgumentException("Innermost",
                    new Exception("Last"))));
        var result = exception.InnermostException<NullReferenceException>();
        Assert.Null(result);
    }
}