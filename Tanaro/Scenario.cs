using Microsoft.Azure.Functions.Worker;

namespace Tanaro;

/// <summary>
/// Records a single deferred invocation of a void/Task-returning <c>[Function]</c> method. A generated subclass
/// exposes an <c>Execute(...)</c> method (matching the real method's parameters) that calls <see cref="Record"/>;
/// <see cref="FunctionHost"/> resolves <typeparamref name="TFunction"/> and awaits the recorded invocation.
/// </summary>
public class Scenario<TFunction>(FunctionContext functionContext)
    where TFunction : class
{
    public FunctionContext FunctionContext { get; } = functionContext;

    internal Func<TFunction, FunctionContext, Task>? Invocation { get; private set; }

    protected void Record(Func<TFunction, FunctionContext, Task> invocation)
    {
        if (Invocation is not null)
        {
            throw new InvalidOperationException("Execute was already called on this scenario.");
        }

        Invocation = invocation;
    }

    public Scenario<TFunction> WithContext(Action<FunctionContext> configure)
    {
        configure(FunctionContext);
        return this;
    }

    public Scenario<TFunction> WithBindingData(string key, object? value)
    {
        ((DummyBindingContext)FunctionContext.BindingContext).Set(key, value);
        return this;
    }

    public Scenario<TFunction> WithRetryContext(int retryCount, int maxRetryCount)
    {
        ((DummyRetryContext)FunctionContext.RetryContext).Set(retryCount, maxRetryCount);
        return this;
    }
}

/// <summary>
/// Records a single deferred invocation of a value-returning <c>[Function]</c> method. A generated subclass
/// exposes an <c>Execute(...)</c> method (matching the real method's parameters) that calls <see cref="Record"/>;
/// <see cref="FunctionHost"/> resolves <typeparamref name="TFunction"/> and awaits the recorded invocation.
/// </summary>
public class Scenario<TFunction, TResult>(FunctionContext functionContext)
    where TFunction : class
{
    public FunctionContext FunctionContext { get; } = functionContext;

    internal Func<TFunction, FunctionContext, Task<TResult>>? Invocation { get; private set; }

    protected void Record(Func<TFunction, FunctionContext, Task<TResult>> invocation)
    {
        if (Invocation is not null)
        {
            throw new InvalidOperationException("Execute was already called on this scenario.");
        }

        Invocation = invocation;
    }

    public Scenario<TFunction, TResult> WithBindingData(string key, object? value)
    {
        ((DummyBindingContext)FunctionContext.BindingContext).Set(key, value);
        return this;
    }

    public Scenario<TFunction, TResult> WithRetryContext(int retryCount, int maxRetryCount)
    {
        ((DummyRetryContext)FunctionContext.RetryContext).Set(retryCount, maxRetryCount);
        return this;
    }

    public Scenario<TFunction, TResult> WithContext(Action<FunctionContext> configure)
    {
        configure(FunctionContext);
        return this;
    }
}

