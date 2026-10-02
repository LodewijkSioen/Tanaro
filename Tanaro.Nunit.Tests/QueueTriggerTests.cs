using Azure.Storage.Queues.Models;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class QueueTriggerTests
{
    [Test]
    public async Task StringShapeReturnsMessage()
    {
        var result = await AppUnderTest.Host.Run<QueueFunction>().QueueString(s => s.Execute("hello"));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task BytesShapeReturnsDecodedMessage()
    {
        var result = await AppUnderTest.Host.Run<QueueFunction>()
            .QueueBytes(s => s.Execute(System.Text.Encoding.UTF8.GetBytes("hello")));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task PocoShapeReturnsId()
    {
        var result = await AppUnderTest.Host.Run<QueueFunction>().QueuePoco(s => s.Execute(new QueueOrder("123")));
        Assert.That(result.Value, Is.EqualTo("123"));
    }

    [Test]
    public async Task BinaryDataShapeReturnsMessage()
    {
        var result = await AppUnderTest.Host.Run<QueueFunction>()
            .QueueBinaryData(s => s.Execute(BinaryData.FromString("hello")));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task QueueMessageShapeReturnsMessageText()
    {
        var message = QueuesModelFactory.QueueMessage(
            messageId: "1",
            popReceipt: "pr",
            messageText: "hello",
            dequeueCount: 1,
            insertedOn: DateTimeOffset.UtcNow,
            expiresOn: DateTimeOffset.UtcNow.AddDays(1),
            nextVisibleOn: DateTimeOffset.UtcNow);

        var result = await AppUnderTest.Host.Run<QueueFunction>().QueueMessage(s => s.Execute(message));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var result = await AppUnderTest.Host.Run<QueueFunction>().QueueString(s => s.Execute("hello"));

        var binding = result.FunctionContext.FunctionDefinition.InputBindings["message"];
        Assert.That(binding.Type, Is.EqualTo("queueTrigger"));
    }
}
