using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;

namespace Tanaro;

public class FunctionNotInvokedException : Exception;

/// <summary>
/// Wraps the outcome of invoking a value-returning <c>[Function]</c> method through <see cref="FunctionHost"/>,
/// so callers can assert on success/failure without a thrown exception escaping the scenario call.
/// </summary>
public sealed class ScenarioResult<TResult>
{
    internal ScenarioResult(TResult? value, Exception? exception, bool invoked, FunctionContext functionContext)
    {
        // Suppressed: before a successful invocation there is no value yet, regardless of TResult's own nullability.
        Value = value!;
        Exception = exception;
        Invoked = invoked;
        FunctionContext = functionContext;
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

    // Always populated - even a short-circuited or failed invocation still ran through a real FunctionContext.
    public FunctionContext FunctionContext { get; }

    public bool Succeeded => Invoked && Exception is null;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Faulted => Exception is not null;

    public void EnsureSuccess()
    {
        if (Faulted)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Exception).Throw();
        }
        if (!Succeeded) throw new FunctionNotInvokedException();
    }
}

/// <summary>
/// Wraps the outcome of invoking a void/Task-returning <c>[Function]</c> method through <see cref="FunctionHost"/>,
/// so callers can assert on success/failure without a thrown exception escaping the scenario call.
/// </summary>
public sealed class ScenarioResult
{
    internal ScenarioResult(Exception? exception, bool invoked, FunctionContext functionContext)
    {
        Exception = exception;
        Invoked = invoked;
        FunctionContext = functionContext;
    }

    public Exception? Exception { get; }

    // True once the function method itself started running, even if it then threw.
    public bool Invoked { get; }

    // Always populated - even a short-circuited or failed invocation still ran through a real FunctionContext.
    public FunctionContext FunctionContext { get; }

    public bool Succeeded => Invoked && Exception is null;

    [MemberNotNullWhen(true, nameof(Exception))]
    public bool Faulted => Exception is not null;

    public void EnsureSuccess()
    {
        if (Faulted)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Exception).Throw();
        }
        if (!Invoked) throw new FunctionNotInvokedException();
    }
}
