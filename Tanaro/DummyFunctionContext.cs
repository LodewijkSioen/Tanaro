using Microsoft.Azure.Functions.Worker;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Tanaro;

public class DummyFunctionContext(TraceContext traceContext, IServiceProvider instanceServices, FunctionDefinition functionDefinition) : FunctionContext
{
    public override string InvocationId { get; } = Guid.NewGuid().ToString();
    public override string FunctionId { get; } = functionDefinition.Id;
    public override TraceContext TraceContext { get; } = traceContext;
    public override BindingContext BindingContext { get; } = new DummyBindingContext();
    public override RetryContext RetryContext { get; } = new DummyRetryContext();
    public override IServiceProvider InstanceServices { get; set; } = instanceServices;
    public override FunctionDefinition FunctionDefinition { get; } = functionDefinition;
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
    public override IInvocationFeatures Features { get; } = new DummyInvocationFeatures();

    private readonly Dictionary<string, object?> _outputBindingData = [];

    // Populated by OutputBindingCapture after invocation; only contains entries for property-based (multi-output) bindings.
    public IReadOnlyDictionary<string, object?> OutputBindingData => _outputBindingData;

    internal void SetOutputBinding(string name, object? value) => _outputBindingData[name] = value;
}

public class DummyTraceContext(Activity? activity) : TraceContext
{
    public override string TraceParent { get; } = activity?.Id ?? string.Empty;
    public override string TraceState { get; } = activity?.TraceStateString ?? string.Empty;
}

public class DummyBindingContext : BindingContext
{
    private readonly Dictionary<string, object?> _bindingData = [];

    public override IReadOnlyDictionary<string, object?> BindingData => _bindingData;

    internal void Set(string key, object? value) => _bindingData[key] = value;
}

public class DummyRetryContext : RetryContext
{
    private int _retryCount;
    private int _maxRetryCount;

    public override int RetryCount => _retryCount;
    public override int MaxRetryCount => _maxRetryCount;

    internal void Set(int retryCount, int maxRetryCount)
    {
        _retryCount = retryCount;
        _maxRetryCount = maxRetryCount;
    }
}

public class DummyBindingMetadata(string name, string type, BindingDirection direction) : BindingMetadata
{
    public override string Name { get; } = name;
    public override string Type { get; } = type;
    public override BindingDirection Direction { get; } = direction;
}

public class DummyFunctionDefinition(
    string name = "",
    string id = "",
    string entryPoint = "",
    string pathToAssembly = "",
    IImmutableDictionary<string, BindingMetadata>? inputBindings = null,
    IImmutableDictionary<string, BindingMetadata>? outputBindings = null,
    ImmutableArray<FunctionParameter> parameters = default) : FunctionDefinition
{
    public override string Id { get; } = id;
    public override string Name { get; } = name;
    public override string PathToAssembly { get; } = pathToAssembly;
    public override string EntryPoint { get; } = entryPoint;
    public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = inputBindings ?? ImmutableDictionary<string, BindingMetadata>.Empty;
    public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = outputBindings ?? ImmutableDictionary<string, BindingMetadata>.Empty;
    public override ImmutableArray<FunctionParameter> Parameters { get; } = parameters.IsDefault ? ImmutableArray<FunctionParameter>.Empty : parameters;
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