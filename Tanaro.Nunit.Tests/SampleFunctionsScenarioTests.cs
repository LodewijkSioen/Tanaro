using Azure.Messaging.EventHubs;
using Tanaro.DemoFunction;
using Tanaro.Generated;

namespace Tanaro.Nunit.Tests;

public class SampleFunctionsScenarioTests
{
    [Test]
    public async Task TaskOfValueShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleTaskOfValue(s => s.Execute([eventData]));
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    public async Task TaskShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.For<SampleFunctions>().SampleTask(s => s.Execute([eventData]));
    }

    [Test]
    public async Task VoidShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.For<SampleFunctions>().SampleVoid(s => s.Execute([eventData]));
    }

    [Test]
    public async Task FunctionContextShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleWithContext(s => s.Execute([eventData]));
        Assert.That(result, Does.StartWith("1:"));
    }

    [Test]
    public async Task WithContextMutationIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        // WithContext is called after Execute in source, but the recorded invocation only runs once this
        // configure lambda returns, so the mutation is still visible to the function.
        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleWithContextItem(s =>
        {
            s.Execute([eventData])
                .WithContext(ctx => ctx.Items.Add("test", "item"));
        });

        Assert.That(result, Is.EqualTo("item"));
    }
}

