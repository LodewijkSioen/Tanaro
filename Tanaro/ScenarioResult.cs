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
        // Suppressed: before a successful invocation there is no value yet, regardless of TResult's own nullability.
        Value = value!;
        Exception = exception;
        Invoked = invoked;
    }

    // Typed as bare TResult (not TResult?) so a nullable-returning function's TResult already carries the '?' -
    // callers get a real nullable-dereference warning for a legitimate null result, instead of a blanket promise.
    public TResult Value
    {
        get
        {
            EnsureSuccess();
            return field;
        }
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
