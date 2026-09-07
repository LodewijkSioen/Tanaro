using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

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


public class FunctionApplicationFactory<T> : IFunctionApplicationFactory
    where T : class
{
    private readonly IHost _host;

    public FunctionApplicationFactory(Action<IHostBuilder> configuration, IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var factory = HostFactoryResolver.ResolveHostFactory(typeof(T).Assembly, hostBuilder =>
        {
            hostBuilder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(settings));
            hostBuilder.ConfigureServices(s => s.RemoveAll<IHostedService>());
            configuration(hostBuilder);
        });

        if (factory is null)
        {
            throw new InvalidOperationException($"Could not find an entry point on the assembly of {typeof(T)}.");
        }

        _host = factory([]);
    }

    public IServiceProvider Services => _host.Services;

    public void Dispose() => _host.Dispose();

    public ValueTask DisposeAsync() => _host is IAsyncDisposable disposable ? disposable.DisposeAsync() : new(Task.Run(_host.Dispose));
}


public interface IFunctionApplicationFactory : IDisposable, IAsyncDisposable
{
    IServiceProvider Services { get; }
}

public class Metrics
{
    public const string TelemetryName = "Tanaro.Metrics";
    internal static readonly ActivitySource Source = new(TelemetryName);
}

public class DummyTraceContext(Activity? activity) : TraceContext
{
    public override string TraceParent { get; } = activity?.Id ?? string.Empty;
    public override string TraceState { get; } = activity?.TraceStateString ?? string.Empty;
}

public class DummyFunctionContext(TraceContext traceContext) : FunctionContext
{
    public override string InvocationId { get; } = Guid.NewGuid().ToString();
    public override string FunctionId { get; } = string.Empty;
    public override TraceContext TraceContext { get; } = traceContext;
    public override BindingContext BindingContext { get; } = new DummyBindingContext();
    public override RetryContext RetryContext { get; } = new DummyRetryContext();
    public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
    public override FunctionDefinition FunctionDefinition { get; } = new DummyFunctionDefinition();
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
    public override IInvocationFeatures Features { get; } = new DummyInvocationFeatures();
}

public class DummyBindingContext : BindingContext
{
    public override IReadOnlyDictionary<string, object?> BindingData { get; } = new Dictionary<string, object?>();
}

public class DummyRetryContext : RetryContext
{
    public override int RetryCount { get; } = 0;
    public override int MaxRetryCount { get; } = 0;
}

public class DummyFunctionDefinition : FunctionDefinition
{
    public override string Id { get; } = string.Empty;
    public override string Name { get; } = string.Empty;
    public override string PathToAssembly { get; } = string.Empty;
    public override string EntryPoint { get; } = string.Empty;
    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
    public override ImmutableArray<FunctionParameter> Parameters { get; } = ImmutableArray<FunctionParameter>.Empty;
}

public class DummyInvocationFeatures : IInvocationFeatures
{
    private readonly Dictionary<Type, object> _features = [];

    public T Get<T>() => _features.TryGetValue(typeof(T), out var value) ? (T)value : default!;

    public void Set<T>(T instance)
    {
        if (instance is null)
        {
            _features.Remove(typeof(T));
        }
        else
        {
            _features[typeof(T)] = instance;
        }
    }

    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}