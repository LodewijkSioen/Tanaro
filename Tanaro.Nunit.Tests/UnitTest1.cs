using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;
using Tanaro.Generated;

namespace Tanaro.Nunit.Tests;

[SetUpFixture]
[FunctionUnderTest<EventHubFunction>]
[FunctionUnderTest<SampleFunctions>]
public class AppUnderTest
{
    public static FunctionHost Host { get; private set; } = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        Host = FunctionHost.For<Program>(builder =>
        {

        }, []) ;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Host.Dispose();
    }
}

public class EventHubTriggerTest
{
    [Test]
    public async Task TestInvovation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("Hello World"));

        var result = await AppUnderTest.Host.EventHubFunction([eventData]);
        Assert.That(result, Is.EqualTo("Hello World"));
    }
}

public class SampleFunctionsScenarioTests
{
    [Test]
    public async Task TaskOfValueShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.SampleTaskOfValue([eventData]);
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public async Task TaskShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.SampleTask([eventData]);
    }

    [Test]
    public async Task VoidShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.SampleVoid([eventData]);
    }

    [Test]
    public async Task FunctionContextShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.SampleWithContext([eventData]);
        Assert.That(result, Does.StartWith("1:"));
    }
}