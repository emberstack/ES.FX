using ES.FX.Hosting.Lifetime;

namespace ES.FX.Hosting.Tests
{
    public class ControlledExitExceptionTests
    {
        public const string message = "message";
        [Fact]
        public void TestCtorWithMessage()
        {
            var exception = new ControlledExitException(message);
            Assert.Equal(message, exception.Message);
        }

        [Fact]
        public void TestCtorWithMessageAndInnerException()
        {
            var innerException = new Exception(message);
            var exception = new ControlledExitException(message, innerException);
            Assert.Equal(innerException, exception.InnerException);
        }
    }
}
