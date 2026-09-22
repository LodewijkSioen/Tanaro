using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tanaro.Nunit.Tests;

/// <summary>
/// Demonstrates that log capture is entirely consumer-owned: this provider lives in the test project (not
/// Tanaro) and is registered via <c>FunctionHost.Run</c>'s <c>configuration</c> callback. It reads
/// <see cref="LoggingCorrelation.InvocationIdKey"/> out of the scope Tanaro pushes around each invocation so
/// entries can be filtered back to a specific scenario call even though this provider is a singleton shared
/// across every test that uses <see cref="AppUnderTest.Host"/>.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentBag<CapturedLogEntry> _entries = [];
    private IExternalScopeProvider? _scopeProvider;

    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries;

    public IReadOnlyList<CapturedLogEntry> EntriesFor(string invocationId) =>
        _entries.Where(e => e.InvocationId == invocationId).ToList();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string? invocationId = null;
            provider._scopeProvider?.ForEachScope((scopeValue, _) =>
            {
                if (scopeValue is IReadOnlyDictionary<string, object?> scope &&
                    scope.TryGetValue(LoggingCorrelation.InvocationIdKey, out var value))
                {
                    invocationId = value as string;
                }
            }, (object?)null);

            provider._entries.Add(new CapturedLogEntry(invocationId, logLevel, category, formatter(state, exception)));
        }
    }
}

public sealed record CapturedLogEntry(string? InvocationId, LogLevel Level, string Category, string Message);
