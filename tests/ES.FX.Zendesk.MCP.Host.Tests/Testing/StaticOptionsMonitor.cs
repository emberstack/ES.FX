using Microsoft.Extensions.Options;

namespace ES.FX.Zendesk.MCP.Host.Tests.Testing;

/// <summary>
///     A minimal <see cref="IOptionsMonitor{T}" /> returning a fixed value, for unit tests.
/// </summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}