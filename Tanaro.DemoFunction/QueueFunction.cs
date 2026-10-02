using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

public class QueueFunction
{
    [Function("QueueString")]
    public string RunString([QueueTrigger("queue")] string message) => message;

    [Function("QueueBytes")]
    public string RunBytes([QueueTrigger("queue")] byte[] message) => System.Text.Encoding.UTF8.GetString(message);

    [Function("QueuePoco")]
    public string RunPoco([QueueTrigger("queue")] QueueOrder order) => order.Id;

    [Function("QueueBinaryData")]
    public string RunBinaryData([QueueTrigger("queue")] BinaryData message) => message.ToString();

    [Function("QueueMessage")]
    public string RunQueueMessage([QueueTrigger("queue")] QueueMessage message) => message.MessageText;
}

public record QueueOrder(string Id);
