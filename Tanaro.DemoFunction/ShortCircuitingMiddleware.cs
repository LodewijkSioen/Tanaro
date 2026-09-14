using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Tanaro.DemoFunction;

// Lets a test opt a scenario into short-circuiting by setting context.Items["shortCircuit"] beforehand.
public class ShortCircuitingMiddleware : IFunctionsWorkerMiddleware
{
    public Task Invoke(FunctionContext context, FunctionExecutionDelegate next) =>
        context.Items.ContainsKey("shortCircuit") ? Task.CompletedTask : next(context);
}
