using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace ES.FX.Additions.Serilog.Tests;

/// <summary>
///     A simple in-memory Serilog sink that captures every emitted <see cref="LogEvent" /> for assertions.
/// </summary>
internal sealed class CapturingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyList<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
