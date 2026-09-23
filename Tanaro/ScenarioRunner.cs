using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Tanaro;

// Owns the actual scope/pipeline execution behind FunctionHost.Runner, invoked by generated extension methods.
// Public (not internal) because generated hook methods live in the consumer's assembly.
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ScenarioRunner
{
    private readonly IServiceProvider _services;

    internal ScenarioRunner(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<ScenarioResult<TResult>> RunScenario<TFunction, TResult, TScenario>(Action<TScenario> configure, Func<DummyFunctionContext, TScenario> createScenario, FunctionDefinition definition)
        where TFunction : class
        where TScenario : Scenario<TFunction, TResult>
    {
        var (scenario, exception, invoked) = await RunCore<TFunction, TScenario>(configure, createScenario, definition);

        ScenarioResult<TResult> result;
        if (exception is not null)
        {
            result = new ScenarioResult<TResult>(default, exception, invoked);
        }
        else if (!invoked)
        {
            result = new ScenarioResult<TResult>(default, null, invoked: false);
        }
        else
        {
            OutputBindingCapture.Capture(definition, scenario.Result, scenario.DummyFunctionContext);
            result = new ScenarioResult<TResult>(scenario.Result, null, invoked: true);
        }

        if (!scenario.ExpectsFailure)
        {
            result.EnsureSuccess();
        }

        return result;
    }

    public async Task<ScenarioResult> RunScenario<TFunction, TScenario>(Action<TScenario> configure, Func<DummyFunctionContext, TScenario> createScenario, FunctionDefinition definition)
        where TFunction : class
        where TScenario : Scenario<TFunction>
    {
        var (scenario, exception, invoked) = await RunCore<TFunction, TScenario>(configure, createScenario, definition);
        var result = new ScenarioResult(exception, invoked);

        if (!scenario.ExpectsFailure)
        {
            result.EnsureSuccess();
        }

        return result;
    }

    private async Task<(TScenario Scenario, Exception? Exception, bool Invoked)> RunCore<TFunction, TScenario>(Action<TScenario> configure, Func<DummyFunctionContext, TScenario> createScenario, FunctionDefinition definition)
        where TFunction : class
        where TScenario : Scenario<TFunction>
    {
        using var scope = _services.CreateScope();
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
            return (scenario, ex, invoked);
        }

        return (scenario, null, invoked);
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
}
