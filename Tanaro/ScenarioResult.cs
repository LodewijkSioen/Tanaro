using Google.Protobuf.WellKnownTypes;
using System.Diagnostics.CodeAnalysis;

namespace Tanaro;

public class FunctionNotInvokedException : Exception;

/// <summary>
/// Wraps the outcome of invoking a value-returning <c>[Function]</c> method through <see cref="FunctionHost"/>,
/// so callers can assert on success/failure without a thrown exception escaping the scenario call.
/// </summary>
public sealed class ScenarioResult<TResult>
{
    internal ScenarioResult(TResult? value, Exception? exception, bool invoked)
    {
        Value = value;
        Exception = exception;
        Invoked = invoked;
    }

    public TResult? Value { get; }

    public Exception? Exception { get; }

    // True once the function method itself started running, even if it then threw.
    public bool Invoked { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool Succeeded => Invoked && Exception is null;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Faulted => Exception is not null;

    [MemberNotNull(nameof(Value))]
    public void EnsureSuccess()
    {
        if (Faulted) throw Exception;
        if (!Succeeded) throw new FunctionNotInvokedException();
    }
}

/// <summary>
/// Wraps the outcome of invoking a void/Task-returning <c>[Function]</c> method through <see cref="FunctionHost"/>,
/// so callers can assert on success/failure without a thrown exception escaping the scenario call.
/// </summary>
public sealed class ScenarioResult
{
    internal ScenarioResult(Exception? exception, bool invoked)
    {
        Exception = exception;
        Invoked = invoked;
    }

    public Exception? Exception { get; }

    // True once the function method itself started running, even if it then threw.
    public bool Invoked { get; }

    public bool Succeeded => Invoked && Exception is null;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Faulted => Exception is not null;

    public void EnsureSuccess()
    {
        if (Faulted) throw Exception;
        if (!Invoked) throw new FunctionNotInvokedException();
    }
}
