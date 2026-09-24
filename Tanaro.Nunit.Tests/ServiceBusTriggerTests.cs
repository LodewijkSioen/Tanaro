using Azure.Messaging.ServiceBus;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class ServiceBusTriggerTests
{
    [Test]
    public async Task StringShapeReturnsMessage()
    {
        var result = await AppUnderTest.Host.Run<ServiceBusFunction>().ServiceBusString(s => s.Execute("hello"));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task PocoShapeReturnsId()
    {
        var result = await AppUnderTest.Host.Run<ServiceBusFunction>()
            .ServiceBusPoco(s => s.Execute(new ServiceBusOrder("123")));
        Assert.That(result.Value, Is.EqualTo("123"));
    }

    [Test]
    public async Task MessageShapeReturnsBody()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("Hello World"));

        var result = await AppUnderTest.Host.Run<ServiceBusFunction>().ServiceBusMessage(s => s.Execute(message));
        Assert.That(result.Value, Is.EqualTo("Hello World"));
    }

    [Test]
    public async Task BatchShapeReturnsCount()
    {
        var messages = new[]
        {
            ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("one")),
            ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("two")),
        };

        var result = await AppUnderTest.Host.Run<ServiceBusFunction>().ServiceBusBatch(s => s.Execute(messages));
        Assert.That(result.Value, Is.EqualTo("2"));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var result = await AppUnderTest.Host.Run<ServiceBusFunction>().ServiceBusString(s => s.Execute("hello"));

        var binding = result.FunctionContext.FunctionDefinition.InputBindings["message"];
        Assert.That(binding.Type, Is.EqualTo("serviceBusTrigger"));
    }
}
