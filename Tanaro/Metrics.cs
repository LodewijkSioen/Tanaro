using System.Diagnostics;

namespace Tanaro;

public class Metrics
{
    public const string TelemetryName = "Tanaro.Metrics";
    internal static readonly ActivitySource Source = new(TelemetryName);
}