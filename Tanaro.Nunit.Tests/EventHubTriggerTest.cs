using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;
using Tanaro.Generated;

namespace Tanaro.Nunit.Tests;

public class EventHubTriggerTest
{
    [Test]
    public async Task TestInvovation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("Hello World"));

        var result = await AppUnderTest.Host
            .For<EventHubFunction>()
            .EventHubFunction(s => s.Execute([eventData]));
        Assert.That(result.Value, Is.EqualTo("Hello World"));
    }
}