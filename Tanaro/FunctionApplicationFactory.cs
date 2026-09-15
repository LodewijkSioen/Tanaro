using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Tanaro;

public interface IFunctionApplicationFactory : IDisposable, IAsyncDisposable
{
    IServiceProvider Services { get; }
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