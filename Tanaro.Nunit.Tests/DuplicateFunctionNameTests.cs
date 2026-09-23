using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Tanaro.Nunit.Tests;

[FunctionUnderTest<DuplicateOne>]
[FunctionUnderTest<DuplicateTwo>]
public class DuplicateFunctionNameTests
{
    public class DuplicateOne
    {
        [Function("FunctionName")]
        public int DoSomething()
        {
            return 123;
        }
    }

    public class DuplicateTwo
    {
        [Function("FunctionName")]
        public int DoSomething()
        {
            return 123;
        }
    }

    [Test]
    public async Task TestDuplicateFunctions()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        await using var host = FunctionHost.For(builder, []);

        // Without this line the Activity will not be started and the TraceContext will be empty.
        _ = host.Services.GetService<TracerProvider>();

        await host.Run<DuplicateOne>().FunctionName(s => s.Execute());
        await host.Run<DuplicateTwo>().FunctionName(s => s.Execute());

        Assert.Pass();
    }
}