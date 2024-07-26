using ES.FX.Hosting.Lifetime;
using ES.FX.Shared.Tests.Utils;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests
{
    public class ProgramEntryTests
    {

        [Theory]
        [InlineData(20, 10)]
        public async Task TestNoError(int exitCode, int exitActionsCount)
        {
            var exitActions = GetExitActions(exitActionsCount);

            var mock = new Mock<ILogger<ProgramEntry>>();

            ProgramEntry x = new(mock.Object, exitActions.actionsList, new ProgramEntryOptions());

            var result = await x.RunAsync(_ => Task.FromResult(exitCode));

            Assert.Equal(exitCode, result);

            LoggerTestHelper.VerifyLoggerWasCalled(mock, "Program completed");
            exitActions.funcMock.Verify(funcMock => funcMock(It.IsAny<ProgramEntryOptions>()), Times.Exactly(exitActionsCount));
        }

        [Theory]
        [InlineData(10, 10)]
        public async Task TestControlledExit(int exitCode, int exitActionsCount)
        {
            var exitActions = GetExitActions(exitActionsCount);

            var mock = new Mock<ILogger<ProgramEntry>>();

            ProgramEntry x = new(mock.Object, exitActions.actionsList, new ProgramEntryOptions());

            var result = await x.RunAsync(_ =>
            {
                var controlledExitException = new ControlledExitException();
                controlledExitException.ExitCode = exitCode;
                throw controlledExitException;
            });

            Assert.Equal(exitCode, result);

            LoggerTestHelper.VerifyLoggerWasCalled(mock, "Program exited controlled");
            exitActions.funcMock.Verify(funcMock => funcMock(It.IsAny<ProgramEntryOptions>()), Times.Exactly(exitActionsCount));
        }

        [Theory]
        [InlineData(10)]
        public async Task TestUnexpectedError(int exitActionsCount)
        {
            var exitActions = GetExitActions(exitActionsCount);

            var mock = new Mock<ILogger<ProgramEntry>>();

            ProgramEntry x = new(mock.Object, exitActions.actionsList, new ProgramEntryOptions());

            var result = await x.RunAsync(_ => throw new Exception());

            Assert.Equal(1, result);
            LoggerTestHelper.VerifyLoggerWasCalled(mock, "Program terminated unexpectedly", LogLevel.Critical);
            exitActions.funcMock.Verify(funcMock => funcMock(It.IsAny<ProgramEntryOptions>()), Times.Exactly(exitActionsCount));
        }

        private (List<Func<ProgramEntryOptions, Task>> actionsList, Mock<Func<ProgramEntryOptions, Task>> funcMock) GetExitActions(int exitActionsCount)
        {
            var funcMock = new Mock<Func<ProgramEntryOptions, Task>>();

            List<Func<ProgramEntryOptions, Task>> actionsList = [];
            for (int i = 0; i < exitActionsCount; i++)
            {
                actionsList.Add(funcMock.Object);
            }

            return (actionsList, funcMock);
        }

    }
}