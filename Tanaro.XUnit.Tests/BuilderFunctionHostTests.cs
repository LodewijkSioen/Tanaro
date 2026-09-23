using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Tanaro.Generated;

namespace Tanaro.XUnit.Tests;

public class BuilderFunctionHostTests
{
    [FunctionUnderTest<EmbeddedFunction>]
    public class EmbeddedFunction
    {
        [Function("DoSomething")]
        public int DoSomething() => 1;
    }

    [Fact]
    public async Task RunsFromAPreBuiltFunctionsApplicationBuilder()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        await using var host = FunctionHost.For(builder, []);

        var result = await host.Run<EmbeddedFunction>().DoSomething(s => s.Execute());

        Assert.Equal(1, result.Value);
    }
}
