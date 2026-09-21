namespace Tanaro;

/// <summary>
/// Well-known logging-scope key that <see cref="FunctionHost"/> pushes around every function invocation, so a
/// consumer-registered <c>ILoggerProvider</c> (supplied via the <c>configuration</c> callback on
/// <see cref="FunctionHost.For{T}"/>) can correlate captured log entries back to a specific scenario call.
/// Requires the provider to implement <c>ISupportExternalScope</c> to observe it.
/// </summary>
public static class LoggingCorrelation
{
    public const string InvocationIdKey = "InvocationId";
}
