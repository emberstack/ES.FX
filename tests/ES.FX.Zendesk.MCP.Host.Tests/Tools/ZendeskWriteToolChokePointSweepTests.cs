using System.Reflection;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.MCP.Host.Execution;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Moq;

namespace ES.FX.Zendesk.MCP.Host.Tests.Tools;

/// <summary>
///     Reflection sweep proving EVERY write tool method routes through the <c>ZendeskToolInvoker</c>
///     execution-mode choke point: invoked with a ReadOnly effective mode, each must surface the read-only
///     rejection <see cref="McpException" /> without ever touching the Zendesk client (a strict mock, so any
///     member access — even an area-API property getter — throws and is reported as a failure).
/// </summary>
public class ZendeskWriteToolChokePointSweepTests
{
    private const int ExpectedWriteToolClasses = 12;
    private const int ExpectedWriteTools = 87;

    [Fact]
    public async Task Every_Write_Tool_Rejects_In_ReadOnly_Mode_Without_Touching_The_Client()
    {
        var writeToolTypes = typeof(Program).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null &&
                           type.Name.EndsWith("WriteTools", StringComparison.Ordinal))
            .OrderBy(type => type.Name)
            .ToList();

        Assert.Equal(ExpectedWriteToolClasses, writeToolTypes.Count);

        var executionMode = new Mock<IMcpExecutionModeAccessor>();
        executionMode.SetupGet(accessor => accessor.EffectiveMode).Returns(McpExecutionMode.ReadOnly);

        var failures = new List<string>();
        var checkedTools = 0;

        foreach (var type in writeToolTypes)
        {
            var zendeskClient = new Mock<IZendeskClient>(MockBehavior.Strict);
            var tools = Activator.CreateInstance(type, zendeskClient.Object, executionMode.Object);

            foreach (var method in type
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                     BindingFlags.DeclaredOnly)
                         .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null))
            {
                checkedTools++;
                var display = $"{type.Name}.{method.Name}";
                try
                {
                    var arguments = method.GetParameters().Select(DummyArgument).ToArray();
                    var task = Assert.IsAssignableFrom<Task>(method.Invoke(tools, arguments));
                    await task;
                    failures.Add($"{display}: completed instead of rejecting the write in read-only mode.");
                }
                catch (McpException exception) when (exception.Message.Contains("read-only"))
                {
                    // Expected: the ZendeskToolInvoker gate rejected the write before any client call.
                }
                catch (Exception exception)
                {
                    var actual = exception is TargetInvocationException { InnerException: { } inner }
                        ? inner
                        : exception;
                    failures.Add($"{display}: surfaced {actual.GetType().Name} instead of the read-only " +
                                 $"rejection McpException: {actual.Message}");
                }
            }

            // Belt and braces: the rejection above already proves the gate ran first, but also assert the
            // strict client mock recorded no calls at all.
            zendeskClient.VerifyNoOtherCalls();
        }

        Assert.Equal(ExpectedWriteTools, checkedTools);
        Assert.True(failures.Count == 0,
            $"Write tools escaped the read-only choke point:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    /// <summary>
    ///     Builds a minimal non-null argument for a tool parameter: default value types, empty strings and
    ///     arrays, <c>null</c> for nullables, and default-constructed write models — enough for the eagerly
    ///     evaluated action strings, which only read properties off these arguments, so the call reaches the
    ///     execution-mode gate.
    /// </summary>
    private static object? DummyArgument(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type == typeof(CancellationToken)) return TestContext.Current.CancellationToken;
        if (type == typeof(string)) return string.Empty;
        if (type.IsArray) return Array.CreateInstance(type.GetElementType()!, 0);
        if (Nullable.GetUnderlyingType(type) is not null) return null;
        return Activator.CreateInstance(type);
    }
}
