using ES.FX.Hosting.Lifetime;

namespace ES.FX.Hosting.Tests;

public class ControlledExitExceptionTests
{
    public const string Message = "message";

    [Fact]
    public void Ctor_Message()
    {
        var exception = new ControlledExitException(Message);
        Assert.Equal(Message, exception.Message);
    }

    [Fact]
    public void Ctor_MessageAndInnerException()
    {
        var innerException = new Exception(Message);
        var exception = new ControlledExitException(Message, innerException);
        Assert.Equal(innerException, exception.InnerException);
    }
}