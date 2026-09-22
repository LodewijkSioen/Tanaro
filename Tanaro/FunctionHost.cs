using System.ComponentModel;
using Microsoft.Extensions.Hosting;

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
        Runner = new(Services);
    }

    public static FunctionHost For<T>(Action<IHostBuilder> configuration, IEnumerable<KeyValuePair<string, string?>> settings)
        where T : class
    {
        return new(new FunctionApplicationFactory<T>(configuration, settings));
    }

    public FunctionScenarios<TFunction> Run<TFunction>() where TFunction : class => new(this);

    // Public (not internal) because generated hook methods live in the consumer's assembly.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ScenarioRunner Runner { get; }

    public void Dispose()
    {
        _factory.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _factory.DisposeAsync();
    }
}