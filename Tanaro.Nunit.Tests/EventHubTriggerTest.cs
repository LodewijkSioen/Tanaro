using Azure.Messaging.EventHubs;
using Tanaro.Generated;

namespace Tanaro.Nunit.Tests;

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