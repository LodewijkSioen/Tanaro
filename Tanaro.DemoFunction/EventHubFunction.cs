using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Tanaro.DemoFunction;

public class EventHubFunction(ILogger<EventHubFunction> logger)
{
    [Function("EventHubFunction")]
    public string Run([EventHubTrigger("hub")] EventData[] eventData, FunctionContext context)
    {
        var data = eventData.Select(e => e.EventBody.ToString());
        return string.Join(',', data);
    }
}