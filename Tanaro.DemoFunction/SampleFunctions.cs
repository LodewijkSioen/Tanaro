using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

// Exercises the Task<T>, Task, void and FunctionContext-parameter scenario shapes.
public class SampleFunctions
{
    [Function("SampleTaskOfValue")]
    public Task<int> CountAsync([EventHubTrigger("hub")] EventData[] eventData) => Task.FromResult(eventData.Length);

    [Function("SampleTask")]
    public Task NoOpAsync([EventHubTrigger("hub")] EventData[] eventData) => Task.CompletedTask;

    [Function("SampleVoid")]
    public void NoOp([EventHubTrigger("hub")] EventData[] eventData)
    {
    }

    [Function("SampleWithContext")]
    public string WithContext([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context) =>
        $"{eventData.Length}:{context.InvocationId}";

    [Function("SampleWithContextItem")]
    public string WithContextItem([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context) =>
        context.Items.TryGetValue("test", out var value) ? (string)value! : "missing";
}
