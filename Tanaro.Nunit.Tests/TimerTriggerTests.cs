using Microsoft.Azure.Functions.Worker;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class TimerTriggerTests
{
    [Test]
    public async Task PastDueTimerReturnsPastDue()
    {
        var result = await AppUnderTest.Host
            .Run<TimerFunction>()
            .TimerFunction(s => s.Execute(new TimerInfo { IsPastDue = true }));

        Assert.That(result.Value, Is.EqualTo("past-due"));
    }

    [Test]
    public async Task OnTimeTimerReturnsOnTime()
    {
        var result = await AppUnderTest.Host
            .Run<TimerFunction>()
            .TimerFunction(s => s.Execute(new TimerInfo { IsPastDue = false }));

        Assert.That(result.Value, Is.EqualTo("on-time"));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var result = await AppUnderTest.Host
            .Run<TimerFunction>()
            .TimerFunction(s => s.Execute(new TimerInfo()));

        var binding = result.FunctionContext.FunctionDefinition.InputBindings["timer"];
        Assert.That(binding.Type, Is.EqualTo("timerTrigger"));
        Assert.That(binding.Direction, Is.EqualTo(BindingDirection.In));
    }
}
