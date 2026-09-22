using Microsoft.Azure.Functions.Worker;

namespace Tanaro;

/// <summary>
/// Records a single deferred invocation of a void/Task-returning <c>[Function]</c> method. A generated subclass
/// exposes an <c>Execute(...)</c> method (matching the real method's parameters) that calls <see cref="Record"/>;
/// <see cref="FunctionHost"/> resolves <typeparamref name="TFunction"/> and awaits the recorded invocation.
/// </summary>
public class Scenario<TFunction>(DummyFunctionContext functionContext)
    where TFunction : class
{
    // Exposed to ScenarioRunner so it can read output bindings without casting FunctionContext back down.
    internal DummyFunctionContext DummyFunctionContext { get; } = functionContext;

    public FunctionContext FunctionContext => DummyFunctionContext;

    internal Func<TFunction, FunctionContext, Task>? Invocation { get; private protected set; }

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
        DummyFunctionContext.SetBindingData(key, value);
        return this;
    }

    public Scenario<TFunction> WithRetryContext(int retryCount, int maxRetryCount)
    {
        DummyFunctionContext.SetRetryContext(retryCount, maxRetryCount);
        return this;
    }

    public Scenario<TFunction> WithCancellationToken(CancellationToken token)
    {
        DummyFunctionContext.SetCancellationToken(token);
        return this;
    }
}

/// <summary>
/// Records a single deferred invocation of a value-returning <c>[Function]</c> method. A generated subclass
/// exposes an <c>Execute(...)</c> method (matching the real method's parameters) that calls <see cref="Record"/>;
/// <see cref="FunctionHost"/> resolves <typeparamref name="TFunction"/> and awaits the recorded invocation.
/// </summary>
public class Scenario<TFunction, TResult>(DummyFunctionContext functionContext) : Scenario<TFunction>(functionContext)
    where TFunction : class
{
    // Set from within the wrapped Invocation delegate once the real invocation completes.
    internal TResult? Result { get; private set; }

    protected void Record(Func<TFunction, FunctionContext, Task<TResult>> invocation)
    {
        if (Invocation is not null)
        {
            throw new InvalidOperationException("Execute was already called on this scenario.");
        }

        Invocation = async (f, ctx) => Result = await invocation(f, ctx);
    }

    // Hides the base members to keep the fluent chain typed as Scenario<TFunction, TResult>.
    public new Scenario<TFunction, TResult> WithBindingData(string key, object? value)
    {
        base.WithBindingData(key, value);
        return this;
    }

    public new Scenario<TFunction, TResult> WithRetryContext(int retryCount, int maxRetryCount)
    {
        base.WithRetryContext(retryCount, maxRetryCount);
        return this;
    }

    public new Scenario<TFunction, TResult> WithCancellationToken(CancellationToken token)
    {
        base.WithCancellationToken(token);
        return this;
    }

    public new Scenario<TFunction, TResult> WithContext(Action<FunctionContext> configure)
    {
        base.WithContext(configure);
        return this;
    }
}

