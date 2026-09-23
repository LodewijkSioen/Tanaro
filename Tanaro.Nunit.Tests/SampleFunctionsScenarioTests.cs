using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class SampleFunctionsScenarioTests
{
    [Test]
    public async Task TaskOfValueShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleTaskOfValue(s => s.Execute([eventData]));
        Assert.That(result.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task TaskShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.Run<SampleFunctions>().SampleTask(s => s.Execute([eventData]));
    }

    [Test]
    public async Task VoidShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        await AppUnderTest.Host.Run<SampleFunctions>().SampleVoid(s => s.Execute([eventData]));
    }

    [Test]
    public async Task FunctionContextShape()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithContext(s => s.Execute([eventData]));
        result.EnsureSuccess();
        Assert.That(result.Value, Does.StartWith("1:"));
    }

    [Test]
    public async Task FunctionDefinitionMetadataIsPopulated()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithContext(s =>
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

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithContext(s =>
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
        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithContextItem(s =>
        {
            s.Execute([eventData])
                .WithContext(ctx => ctx.Items.Add("test", "item"));
        });

        Assert.That(result.Value, Is.EqualTo("item"));
    }

    [Test]
    public async Task MiddlewareStampsContextItemsBeforeInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithMiddlewareItem(s => s.Execute([eventData]));
        Assert.That(result.Value, Is.EqualTo("stamped"));
    }

    [Test]
    public async Task WithBindingDataIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithBindingData(s =>
            s.Execute([eventData]).WithBindingData("test", "bound"));

        Assert.That(result.Value, Is.EqualTo("bound"));
    }

    [Test]
    public async Task WithRetryContextIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithRetryContext(s =>
            s.Execute([eventData]).WithRetryContext(2, 5));

        Assert.That(result.Value, Is.EqualTo("2/5"));
    }

    [Test]
    public async Task CancellationTokenDefaultsToNotCancelled()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithCancellationToken(s => s.Execute([eventData]));

        Assert.That(result.Value, Is.EqualTo("not-cancelled"));
    }

    [Test]
    public async Task WithCancellationTokenIsVisibleDuringInvocation()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleWithCancellationToken(s =>
            s.Execute([eventData]).WithCancellationToken(new CancellationToken(canceled: true)));

        Assert.That(result.Value, Is.EqualTo("cancelled"));
    }

    [Test]
    public async Task ShortCircuitingMiddlewareReportsFunctionNotInvoked()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleTaskOfValue(s =>
            s.Execute([eventData]).WithContext(ctx => ctx.Items["shortCircuit"] = true).ExpectFailure());

        Assert.That(result.Invoked, Is.False);
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task FunctionDefinitionOutputBindingsAreExtractedFromReturnTypeProperties()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithMultiOutput(s =>
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

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithMultiOutput(s =>
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

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithNullOutput(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        Assert.That(((DummyFunctionContext)capturedContext!).OutputBindingData.ContainsKey("Message"), Is.False);
    }

    [Test]
    public async Task ReturnBoundFunctionsDoNotPopulateOutputBindingData()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleReturnBinding(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        Assert.That(result.Value, Is.EqualTo("x"));
        Assert.That(capturedContext!.FunctionDefinition.OutputBindings.ContainsKey("$return"), Is.True);
        Assert.That(((DummyFunctionContext)capturedContext).OutputBindingData, Is.Empty);
    }

    [Test]
    public async Task WarningIsLoggedWhenNoDataReceived()
    {
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithLogging(s =>
            s.Execute([]).WithContext(ctx => capturedContext = ctx));

        var entries = AppUnderTest.Logs.EntriesFor(capturedContext!.InvocationId);
        Assert.That(entries, Has.One.Matches<CapturedLogEntry>(e =>
            e.Level == LogLevel.Warning && e.Message == "No data received"));
    }

    [Test]
    public async Task InfoIsLoggedWhenDataReceived()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? capturedContext = null;

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithLogging(s =>
            s.Execute([eventData]).WithContext(ctx => capturedContext = ctx));

        var entries = AppUnderTest.Logs.EntriesFor(capturedContext!.InvocationId);
        Assert.That(entries, Has.One.Matches<CapturedLogEntry>(e =>
            e.Level == LogLevel.Information && e.Message == "Received 1 events"));
        Assert.That(entries, Has.None.Matches<CapturedLogEntry>(e => e.Level == LogLevel.Warning));
    }

    [Test]
    public async Task LogsFromOneInvocationAreNotVisibleOnAnother()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));
        FunctionContext? emptyInvocation = null;
        FunctionContext? dataInvocation = null;

        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithLogging(s =>
            s.Execute([]).WithContext(ctx => emptyInvocation = ctx));
        await AppUnderTest.Host.Run<SampleFunctions>().SampleWithLogging(s =>
            s.Execute([eventData]).WithContext(ctx => dataInvocation = ctx));

        var emptyEntries = AppUnderTest.Logs.EntriesFor(emptyInvocation!.InvocationId);
        var dataEntries = AppUnderTest.Logs.EntriesFor(dataInvocation!.InvocationId);

        Assert.That(emptyEntries, Has.All.Matches<CapturedLogEntry>(e => e.Level == LogLevel.Warning));
        Assert.That(dataEntries, Has.All.Matches<CapturedLogEntry>(e => e.Level == LogLevel.Information));
    }

    [Test]
    public async Task FunctionThrowingExceptionIsCapturedOnResult()
    {
        var eventData = EventHubsModelFactory.EventData(BinaryData.FromString("x"));

        var result = await AppUnderTest.Host.Run<SampleFunctions>().SampleThatThrows(s => s.Execute([eventData]).ExpectFailure());

        Assert.That(result.Faulted, Is.True);
        Assert.That(result.Invoked, Is.True);
        Assert.That(result.Exception, Is.InstanceOf<InvalidOperationException>().And.Message.EqualTo("Boom"));
    }
}