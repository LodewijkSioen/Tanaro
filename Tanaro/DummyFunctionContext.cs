using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Tanaro;

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

public class DummyTraceContext(Activity? activity) : TraceContext
{
    public override string TraceParent { get; } = activity?.Id ?? string.Empty;
    public override string TraceState { get; } = activity?.TraceStateString ?? string.Empty;
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