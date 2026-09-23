using System.ComponentModel;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Tanaro;

public class FunctionHost
    : IDisposable, IAsyncDisposable
{
    private readonly IHost _host;

    public IServiceProvider Services { get; }

    private FunctionHost(IHost host)
    {
        _host = host;
        Services = host.Services;
        Runner = new(Services);
    }

    public static FunctionHost For<T>(Action<IHostBuilder> configuration, IEnumerable<KeyValuePair<string, string?>> settings)
        where T : class
    {
        return new(FunctionApplicationFactory.CreateHost<T>(configuration, settings));
    }

    public static FunctionHost For(FunctionsApplicationBuilder builder, IEnumerable<KeyValuePair<string, string?>> settings)
    {
        builder.Configuration.AddInMemoryCollection(settings);
        return new(builder.Build());
    }

    public FunctionScenarios<TFunction> Run<TFunction>() where TFunction : class => new(this);

    // Public (not internal) because generated hook methods live in the consumer's assembly.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ScenarioRunner Runner { get; }

    public void Dispose()
    {
        _host.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _host is IAsyncDisposable disposable ? disposable.DisposeAsync() : new(Task.Run(_host.Dispose));
    }
}