using System.Diagnostics;
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

    public FunctionScenarios<TFunction> For<TFunction>() where TFunction : class => new(this);

    // Public (not internal) because generated hook methods live in the consumer's assembly.
    public async Task<TResult> RunScenario<TFunction, TResult, TScenario>(Action<TScenario> configure, Func<FunctionContext, TScenario> createScenario)
        where TFunction : class
        where TScenario : Scenario<TFunction, TResult>
    {
        using var scope = _factory.Services.CreateScope();
        using var rootActivity = StartActivity(scope.ServiceProvider);

        var scenario = createScenario(BuildFunctionContext(rootActivity));
        configure(scenario);

        if (scenario.Invocation is null)
        {
            throw new InvalidOperationException("No function was invoked - call Execute(...) inside the scenario.");
        }

        var function = ActivatorUtilities.GetServiceOrCreateInstance<TFunction>(scope.ServiceProvider);
        return await scenario.Invocation(function, scenario.FunctionContext);
    }

    // Public (not internal) because generated hook methods live in the consumer's assembly.
    public async Task RunScenario<TFunction, TScenario>(Action<TScenario> configure, Func<FunctionContext, TScenario> createScenario)
        where TFunction : class
        where TScenario : Scenario<TFunction>
    {
        using var scope = _factory.Services.CreateScope();
        using var rootActivity = StartActivity(scope.ServiceProvider);

        var scenario = createScenario(BuildFunctionContext(rootActivity));
        configure(scenario);

        if (scenario.Invocation is null)
        {
            throw new InvalidOperationException("No function was invoked - call Execute(...) inside the scenario.");
        }

        var function = ActivatorUtilities.GetServiceOrCreateInstance<TFunction>(scope.ServiceProvider);
        await scenario.Invocation(function, scenario.FunctionContext);
    }

    private static Activity? StartActivity(IServiceProvider services)
    {
        // Need to resolve the TraceProvider once to kickstart tracing
        _ = services.GetService<TracerProvider>();
        return Metrics.Source.StartActivity();
    }

    private static DummyFunctionContext BuildFunctionContext(Activity? rootActivity) =>
        new(new DummyTraceContext(rootActivity));

    public void Dispose()
    {
        _factory.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _factory.DisposeAsync();
    }
}