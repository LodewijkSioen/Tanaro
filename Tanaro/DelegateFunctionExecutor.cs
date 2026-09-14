using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Invocation;

namespace Tanaro;

// Lets FunctionHost hand invocation back to a Scenario's generated delegate from inside the real middleware pipeline.
internal sealed class DelegateFunctionExecutor(Func<FunctionContext, Task> execute) : IFunctionExecutor
{
    public ValueTask ExecuteAsync(FunctionContext context) => new(execute(context));
}
