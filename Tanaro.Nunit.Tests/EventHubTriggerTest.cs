using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class EventHubTriggerTest
{
    [Test]
    public async Task TestInvovation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("Hello World"));

        var result = await AppUnderTest.Host
            .Run<EventHubFunction>()
            .EventHubFunction(s => s.Execute([eventData]));
        Assert.That(result.Value, Is.EqualTo("Hello World"));
    }
}