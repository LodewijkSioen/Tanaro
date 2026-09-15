using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
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
    public async Task FunctionDefinitionMetadataIsPopulated()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.For<SampleFunctions>().SampleWithContext(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        Assert.That(capturedContext!.FunctionDefinition.Name, Is.EqualTo("SampleWithContext"));
        Assert.That(capturedContext.FunctionDefinition.Id, Is.EqualTo("SampleWithContext"));
        Assert.That(capturedContext.FunctionId, Is.EqualTo(capturedContext.FunctionDefinition.Id));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.For<SampleFunctions>().SampleWithContext(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        var binding = capturedContext!.FunctionDefinition.InputBindings["eventData"];
        Assert.That(binding.Type, Is.EqualTo("eventHubTrigger"));
        Assert.That(binding.Direction, Is.EqualTo(BindingDirection.In));
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

    [Test]
    public async Task MiddlewareStampsContextItemsBeforeInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleWithMiddlewareItem(s => s.Execute([eventData]));
        Assert.That(result, Is.EqualTo("stamped"));
    }

    [Test]
    public async Task WithBindingDataIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleWithBindingData(s =>
            s.Execute([eventData]).WithBindingData("test", "bound"));

        Assert.That(result, Is.EqualTo("bound"));
    }

    [Test]
    public async Task WithRetryContextIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleWithRetryContext(s =>
            s.Execute([eventData]).WithRetryContext(2, 5));

        Assert.That(result, Is.EqualTo("2/5"));
    }

    [Test]
    public void ShortCircuitingMiddlewareReportsFunctionNotInvoked()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        Assert.That(
            () => AppUnderTest.Host.For<SampleFunctions>().SampleTaskOfValue(s =>
                s.Execute([eventData]).WithContext(ctx => ctx.Items["shortCircuit"] = true)),
            Throws.InvalidOperationException);
    }

    [Test]
    public async Task FunctionDefinitionOutputBindingsAreExtractedFromReturnTypeProperties()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.For<SampleFunctions>().SampleWithMultiOutput(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        var binding = capturedContext!.FunctionDefinition.OutputBindings["Message"];
        Assert.That(binding.Type, Is.EqualTo("eventHub"));
        Assert.That(binding.Direction, Is.EqualTo(BindingDirection.Out));
        Assert.That(capturedContext.FunctionDefinition.OutputBindings.ContainsKey("Note"), Is.False);
    }

    [Test]
    public async Task OutputBindingValueIsCapturedAfterInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.For<SampleFunctions>().SampleWithMultiOutput(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        var outputBindingData = ((DummyFunctionContext)capturedContext!).OutputBindingData;
        Assert.That(outputBindingData["Message"], Is.EqualTo("x"));
        Assert.That(outputBindingData.ContainsKey("Note"), Is.False);
    }

    [Test]
    public async Task NullOutputBindingValueIsNotCaptured()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.For<SampleFunctions>().SampleWithNullOutput(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        Assert.That(((DummyFunctionContext)capturedContext!).OutputBindingData.ContainsKey("Message"), Is.False);
    }

    [Test]
    public async Task ReturnBoundFunctionsDoNotPopulateOutputBindingData()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        var result = await AppUnderTest.Host.For<SampleFunctions>().SampleReturnBinding(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        Assert.That(result, Is.EqualTo("x"));
        Assert.That(capturedContext!.FunctionDefinition.OutputBindings.ContainsKey("$return"), Is.True);
        Assert.That(((DummyFunctionContext)capturedContext).OutputBindingData, Is.Empty);
    }
}

