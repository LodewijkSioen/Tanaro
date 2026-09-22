using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<ScenarioResult<TResult>> RunScenario<TFunction, TResult, TScenario>(Action<TScenario> configure, Func<FunctionContext, TScenario> createScenario, FunctionDefinition definition)
        where TFunction : class
        where TScenario : Scenario<TFunction, TResult>
    {
        using var scope = _factory.Services.CreateScope();
        using var rootActivity = StartActivity(scope.ServiceProvider);

        var scenario = createScenario(BuildFunctionContext(rootActivity, scope.ServiceProvider, definition));
        configure(scenario);

        if (scenario.Invocation is null)
        {
            throw new InvalidOperationException("No function was invoked - call Execute(...) inside the scenario.");
        }

        var function = ActivatorUtilities.GetServiceOrCreateInstance<TFunction>(scope.ServiceProvider);

        var invoked = false;
        TResult? result = default;
        scenario.FunctionContext.Features.Set<IFunctionExecutor>(new DelegateFunctionExecutor(async ctx =>
        {
            invoked = true;
            result = await scenario.Invocation(function, ctx);
        }));

        try
        {
            using (BeginInvocationScope(scope.ServiceProvider, scenario.FunctionContext.InvocationId))
            {
                await RunPipeline(scope.ServiceProvider, scenario.FunctionContext);
            }
        }
        catch (Exception ex)
        {
            return new ScenarioResult<TResult>(default, ex, invoked);
        }

        if (!invoked)
        {
            return new ScenarioResult<TResult>(default, null, invoked: false);
        }

        OutputBindingCapture.Capture(definition, result, (DummyFunctionContext)scenario.FunctionContext);

        return new ScenarioResult<TResult>(result, null, invoked: true);
    }

    // Public (not internal) because generated hook methods live in the consumer's assembly.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task<ScenarioResult> RunScenario<TFunction, TScenario>(Action<TScenario> configure, Func<FunctionContext, TScenario> createScenario, FunctionDefinition definition)
        where TFunction : class
        where TScenario : Scenario<TFunction>
    {
        using var scope = _factory.Services.CreateScope();
        using var rootActivity = StartActivity(scope.ServiceProvider);

        var scenario = createScenario(BuildFunctionContext(rootActivity, scope.ServiceProvider, definition));
        configure(scenario);

        if (scenario.Invocation is null)
        {
            throw new InvalidOperationException("No function was invoked - call Execute(...) inside the scenario.");
        }

        var function = ActivatorUtilities.GetServiceOrCreateInstance<TFunction>(scope.ServiceProvider);

        var invoked = false;
        scenario.FunctionContext.Features.Set<IFunctionExecutor>(new DelegateFunctionExecutor(async ctx =>
        {
            invoked = true;
            await scenario.Invocation(function, ctx);
        }));

        try
        {
            using (BeginInvocationScope(scope.ServiceProvider, scenario.FunctionContext.InvocationId))
            {
                await RunPipeline(scope.ServiceProvider, scenario.FunctionContext);
            }
        }
        catch (Exception ex)
        {
            return new ScenarioResult(ex, invoked);
        }

        return new ScenarioResult(null, invoked);
    }

    // Runs the app's own registered middleware (if any) plus the SDK's built-in Output/Execution middleware.
    private static async Task RunPipeline(IServiceProvider services, FunctionContext context)
    {
        var pipeline = services.GetRequiredService<FunctionExecutionDelegate>();
        try
        {
            await pipeline(context);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IFunctionBindingsFeature"))
        {
            // The SDK's built-in OutputBindingsMiddleware always runs and needs an internal feature Tanaro doesn't populate; harmless to ignore.
        }
    }

    private static Activity? StartActivity(IServiceProvider services)
    {
        // Need to resolve the TraceProvider once to kickstart tracing
        _ = services.GetService<TracerProvider>();
        return Metrics.Source.StartActivity();
    }

    // Lets a consumer-registered ILoggerProvider (supplying its own log capture) correlate entries to this invocation.
    private static IDisposable? BeginInvocationScope(IServiceProvider services, string invocationId) =>
        services.GetRequiredService<ILoggerFactory>().CreateLogger("Tanaro").BeginScope(new Dictionary<string, object?>
        {
            [LoggingCorrelation.InvocationIdKey] = invocationId,
        });

    private static DummyFunctionContext BuildFunctionContext(Activity? rootActivity, IServiceProvider instanceServices, FunctionDefinition definition) =>
        new(new DummyTraceContext(rootActivity), instanceServices, definition);

    public void Dispose()
    {
        _factory.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _factory.DisposeAsync();
    }
}