using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

public class ServiceBusFunction
{
    [Function("ServiceBusString")]
    public string RunString([ServiceBusTrigger("queue")] string message) => message;

    [Function("ServiceBusPoco")]
    public string RunPoco([ServiceBusTrigger("queue")] ServiceBusOrder order) => order.Id;

    [Function("ServiceBusMessage")]
    public string RunMessage([ServiceBusTrigger("queue")] ServiceBusReceivedMessage message) => message.Body.ToString();

    [Function("ServiceBusBatch")]
    public string RunBatch([ServiceBusTrigger("queue", IsBatched = true)] ServiceBusReceivedMessage[] messages) =>
        messages.Length.ToString();
}

public record ServiceBusOrder(string Id);
