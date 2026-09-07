using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

[SetUpFixture]
[FunctionUnderTest<EventHubFunction>]
[FunctionUnderTest<SampleFunctions>]
public class AppUnderTest
{
    public static FunctionHost Host { get; private set; } = null!;

    [OneTimeSetUp]
    public void Setup()
    {
        Host = FunctionHost.For<Program>(builder =>
        {

        }, []) ;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Host.Dispose();
    }
}