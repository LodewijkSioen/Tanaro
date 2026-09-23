using Tanaro.DemoFunction;

[assembly: AssemblyFixture(typeof(Tanaro.XUnit.Tests.AppFixture))]

namespace Tanaro.XUnit.Tests;

[FunctionUnderTest<SampleFunctions>]
public class AppFixture : IAsyncDisposable
{
    public FunctionHost Host { get; } = FunctionHost.For<Program>(_ => { }, []);

    public ValueTask DisposeAsync() => Host.DisposeAsync();
}
