using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

public class TimerFunction
{
    [Function("TimerFunction")]
    public string Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer) => timer.IsPastDue ? "past-due" : "on-time";
}
