using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;

namespace Tanaro;

public class FunctionHost
    : IDisposable, IAsyncDisposable
{
    private readonly IFunctionApplicationFactory _factory;

    public IServiceProvider Services { get; }

    private FunctionHost(IFunctionApplicationFactory factory)
    {
        _factory = factory;
        Services = _factory.Services;
    }

    public static FunctionHost For<T>(Action<IHostBuilder> configuration, IEnumerable<KeyValuePair<string, string?>> settings)
        where T : class
    {
        return new(new FunctionApplicationFactory<T>(configuration, settings));
    }

    public Task Scenario<TFunction>(Action<TFunction, FunctionContext> execute)
    {
        return Scenario<TFunction>((fnc, ctx) =>
        {
            execute(fnc, ctx);
            return Task.CompletedTask;
        });
    }

    public Task Scenario<TFunction>(Func<TFunction, FunctionContext, Task> execute)
    {
        return RunScenario<TFunction, object?>(async (fnc, ctx) =>
        {
            await execute(fnc, ctx);
            return null;
        });
    }

    public Task<TResult> Scenario<TFunction, TResult>(Func<TFunction, FunctionContext, TResult> execute)
    {
        return Scenario<TFunction, TResult>((fnc, ctx) => Task.FromResult(execute(fnc, ctx)));
    }

    public Task<TResult> Scenario<TFunction, TResult>(Func<TFunction, FunctionContext, Task<TResult>> execute)
    {
        return RunScenario(execute);
    }

    private Task<TResult> RunScenario<TFunction, TResult>(Func<TFunction, FunctionContext, Task<TResult>> execute)
    {
        using var scope = _factory.Services.CreateScope();
        // Need to resolve the TraceProvider once to kickstart tracing
        _ = scope.ServiceProvider.GetService<TracerProvider>();

        using var rootActivity = Metrics.Source.StartActivity();

        var traceContext = new DummyTraceContext(rootActivity);
        var functionContext = new DummyFunctionContext(traceContext);

        var function = ActivatorUtilities.GetServiceOrCreateInstance<TFunction>(scope.ServiceProvider);

        return execute(function, functionContext);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _factory.DisposeAsync();
    }
}