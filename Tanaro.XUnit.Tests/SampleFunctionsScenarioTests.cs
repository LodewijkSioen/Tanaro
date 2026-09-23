using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;
using Tanaro.Generated;

namespace Tanaro.XUnit.Tests;

public class SampleFunctionsScenarioTests(AppFixture fixture)
{
    [Fact]
    public async Task TaskOfValueShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await fixture.Host.Run<SampleFunctions>().SampleTaskOfValue(s => s.Execute([eventData]));

        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task FunctionContextShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await fixture.Host.Run<SampleFunctions>().SampleWithContext(s => s.Execute([eventData]));

        Assert.StartsWith("1:", result.Value);
    }
}
