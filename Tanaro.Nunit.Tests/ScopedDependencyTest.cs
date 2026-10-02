using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Tanaro.Nunit.Tests;

public class ScopedDependencyTest
{
    public class Dependency : IAsyncDisposable
    {
        public int DoSomething(int number) => number;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    [FunctionUnderTest<FunctionWithScopedDependency>]
    public class FunctionWithScopedDependency(Dependency dependency)
    {
        [Function("DoSomething")]
        public int DoSomething() => dependency.DoSomething(1);
    }

    [Test]
    public async Task DisposesDependency()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        builder.Services.AddScoped<Dependency>();
        await using var host = FunctionHost.For(builder, []);

        var result = await host.Run<FunctionWithScopedDependency>().DoSomething(s => s.Execute());

        Assert.That(result.Value, Is.EqualTo(1));
    }
}