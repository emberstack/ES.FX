using ES.FX.Hosting.Lifetime;
using Microsoft.Extensions.Logging;
using Moq;

namespace ES.FX.Hosting.Tests;

/// <summary>
///     Additional coverage for <see cref="ProgramEntryBuilder" /> and
///     <see cref="ProgramEntry.CreateBuilder" />: default-logger fallback, argument propagation, and null-guards.
/// </summary>
public class ProgramEntryBuilderExtraTests
{
    [Fact]
    public async Task Build_WithoutLogger_UsesNonNullDefaultLogger()
    {
        // When WithLogger is never called, Build must construct a real console ILogger<ProgramEntry> rather than
        // pass null. If it passed null, the very first Log call inside RunAsync (LogTrace "Starting Program")
        // would throw a NullReferenceException. A successful run with a returned exit code proves the fallback
        // logger is non-null and usable.
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        var programEntry = builder.Build();

        var result = await programEntry.RunAsync(_ => Task.FromResult(7));

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task Build_WithoutLogger_SignalOverload_UsesNonNullDefaultLogger()
    {
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());
        var programEntry = builder.Build();

        var result = await programEntry.RunAsync((_, _) => Task.FromResult(8));

        Assert.Equal(8, result);
    }

    [Fact]
    public async Task CreateBuilder_PropagatesArgs_ThroughBuild_ToAction()
    {
        var args = new[] { "--flag", "value", "positional" };

        var programEntry = ProgramEntry.CreateBuilder(args)
            .WithLogger(new Mock<ILogger<ProgramEntry>>().Object)
            .Build();

        string[]? observedArgs = null;
        var result = await programEntry.RunAsync(options =>
        {
            observedArgs = options.Args;
            return Task.FromResult(0);
        });

        Assert.Equal(0, result);
        Assert.NotNull(observedArgs);
        Assert.Equal(args, observedArgs);
    }

    [Fact]
    public async Task CreateBuilder_PropagatesArgs_ToSignalOverloadAction()
    {
        var args = new[] { "a", "b" };

        var programEntry = ProgramEntry.CreateBuilder(args)
            .WithLogger(new Mock<ILogger<ProgramEntry>>().Object)
            .Build();

        string[]? observedArgs = null;
        await programEntry.RunAsync((options, _) =>
        {
            observedArgs = options.Args;
            return Task.FromResult(0);
        });

        Assert.Equal(args, observedArgs);
    }

    [Fact]
    public void AddExitAction_Null_ThrowsArgumentNullException()
    {
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());

        Assert.Throws<ArgumentNullException>(() => builder.AddExitAction(null!));
    }

    [Fact]
    public void WithLogger_Null_ThrowsArgumentNullException()
    {
        var builder = new ProgramEntryBuilder(new ProgramEntryOptions());

        Assert.Throws<ArgumentNullException>(() => builder.WithLogger(null!));
    }

    [Fact]
    public async Task RunAsync_FirstOverload_NullAction_ThrowsArgumentNullException()
    {
        var programEntry = new ProgramEntryBuilder(new ProgramEntryOptions())
            .WithLogger(new Mock<ILogger<ProgramEntry>>().Object)
            .Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            programEntry.RunAsync((Func<ProgramEntryOptions, Task<int>>)null!));
    }
}