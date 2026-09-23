using System.Diagnostics;

namespace Tanaro;

public class Metrics
{
    public const string TelemetryName = "Microsoft.Azure.Functions.Worker";
    internal static readonly ActivitySource Source = new(TelemetryName);
}