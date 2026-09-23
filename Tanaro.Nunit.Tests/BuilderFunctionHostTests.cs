using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;

namespace Tanaro.Nunit.Tests;

public class BuilderFunctionHostTests
{
    [FunctionUnderTest<EmbeddedFunction>]
    public class EmbeddedFunction
    {
        [Function("DoSomething")]
        public int DoSomething() => 1;
    }

    [Test]
    public async Task RunsFromAPreBuiltFunctionsApplicationBuilder()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        await using var host = FunctionHost.For(builder, []);
        
        var result = await host.Run<EmbeddedFunction>().DoSomething(s => s.Execute());

        Assert.That(result.Value, Is.EqualTo(1));
    }
}
