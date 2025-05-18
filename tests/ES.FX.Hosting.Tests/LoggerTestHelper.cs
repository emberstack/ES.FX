using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

public static class LoggerTestHelper
{
    public static Mock<ILogger<T>> VerifyLoggerWasCalled<T>(this Mock<ILogger<T>> logger, string expectedMessage = "",
        LogLevel logLevel = LogLevel.Debug)
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        Func<object, Type, bool> state = (v, _) =>
            string.IsNullOrEmpty(expectedMessage) || v.ToString().Contains(expectedMessage);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        logger.Verify(x => x.Log(
            It.Is<LogLevel>(l => l == logLevel),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => state(v, t)),
            It.IsAny<Exception>(),
            It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)));
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.

        return logger;
    }
}