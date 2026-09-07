using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

[SetUpFixture]
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

        var result = await AppUnderTest.Host.Scenario<EventHubFunction, string>((func, ctx) => func.Run([eventData]));
        Assert.That(result, Is.EqualTo("Hello World"));
    }
}