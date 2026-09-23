using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Tanaro.Nunit.Tests;

public class OpenTelemetryTests
{
    [FunctionUnderTest<OtelFunction>]
    public class OtelFunction
    {
        [Function("Otel")]
        public int DoSomething(FunctionContext functionContext)
        {
            return 123;
        }
    }

    [Test]
    public async Task TestOpenTelemetryActivity()
    {
        var builder = FunctionsApplication.CreateBuilder([]);
        builder.Services
            .AddOpenTelemetry()
            .UseFunctionsWorkerDefaults()
            .ConfigureResource(_ => { })
            .WithTracing(c => c
                .AddSource(Metrics.TelemetryName));
        await using var host = FunctionHost.For(builder, []);

        // Without this line the Activity will not be started and the TraceContext will be empty.
        _ = host.Services.GetService<TracerProvider>();

        var result = await host.Run<OtelFunction>().Otel(s => s.Execute());

        var context = result.FunctionContext.TraceContext;
        Assert.That(context.TraceParent, Is.Not.Empty);

        _ = new ActivityLink(ActivityContext.Parse(context.TraceParent, context.TraceState));
    }
}