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
        context.Items.TryGetValue("test", out var value) ? (string)value : "missing";

    [Function("SampleWithMiddlewareItem")]
    public string WithMiddlewareItem([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context) =>
        context.Items.TryGetValue("middleware", out var value) ? (string)value : "missing";

    [Function("SampleWithBindingData")]
    public string WithBindingData([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context) =>
        context.BindingContext.BindingData.TryGetValue("test", out var value) ? (string)value! : "missing";

    [Function("SampleWithRetryContext")]
    public string WithRetryContext([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context) =>
        $"{context.RetryContext.RetryCount}/{context.RetryContext.MaxRetryCount}";

    [Function("SampleReturnBinding")]
    [EventHubOutput("hub")]
    public string ReturnBinding([EventHubTrigger("hub")] EventData[] eventData) =>
        string.Join(',', eventData.Select(e => e.EventBody.ToString()));

    [Function("SampleWithMultiOutput")]
    public MultiOutputResult MultiOutput([EventHubTrigger("hub")] EventData[] eventData) => new()
    {
        Message = string.Join(',', eventData.Select(e => e.EventBody.ToString())),
        Note = "note"
    };

    [Function("SampleWithNullOutput")]
    public MultiOutputResult NullOutput([EventHubTrigger("hub")] EventData[] eventData) => new()
    {
        Message = null,
        Note = "note"
    };
}

public class MultiOutputResult
{
    [EventHubOutput("hub")]
    public string? Message { get; set; }

    public string? Note { get; set; }
}
