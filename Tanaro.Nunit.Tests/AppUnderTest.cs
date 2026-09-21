using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

[SetUpFixture]
[FunctionUnderTest<EventHubFunction>]
[FunctionUnderTest<SampleFunctions>]
public class AppUnderTest
{
    public static FunctionHost Host { get; private set; } = null!;

    public static CapturingLoggerProvider Logs { get; } = new();

    [OneTimeSetUp]
    public void Setup()
    {
        Host = FunctionHost.For<Program>(builder =>
        {
            builder.ConfigureServices((_, services) => services.AddSingleton<ILoggerProvider>(Logs));
        }, []) ;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Host.Dispose();
    }
}