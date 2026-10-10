using ES.FX.Hosting.Lifetime;
using ES.FX.Hosting.Tests.SUT;
using Microsoft.Extensions.Logging;

// Runs the signal-aware ProgramEntry.RunAsync overload in its own process, so tests can deliver a real POSIX
// signal without signalling the test host (which treats SIGINT as "abort the test session").
using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddSimpleConsole()
    .SetMinimumLevel(LogLevel.Debug));

return await ProgramEntry.CreateBuilder(args)
    .WithLogger(loggerFactory.CreateLogger<ProgramEntry>())
    .Build()
    .RunAsync(async (_, cancellationToken) =>
    {
        Console.WriteLine(SutMarkers.Ready);
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return 99; // Unreachable: only a shutdown signal ends the delay.
    });
