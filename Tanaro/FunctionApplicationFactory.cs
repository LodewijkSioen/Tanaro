using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Tanaro;

internal static class FunctionApplicationFactory
{
    public static IHost CreateHost<T>(Action<IHostBuilder> configuration, IEnumerable<KeyValuePair<string, string?>> settings)
        where T : class
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

        return factory([]);
    }
}