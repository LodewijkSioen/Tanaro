using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class NullableResultTests
{
    [Test]
    public async Task TestNonNullableComplexResult()
    {
        var result = await AppUnderTest.Host.Run<SampleFunctions>().WithNonNullableComplexResult(s => s.Execute());

        Assert.That(result.Value.Name, Is.EqualTo("test"));
        Assert.That(result.Value.Number, Is.EqualTo(123));
    }

    [Test]
    public async Task TestNullableComplexResult()
    {
        var result = await AppUnderTest.Host.Run<SampleFunctions>().WithNullableComplexResult(s => s.Execute());

        Assert.That(result.Value, Is.Null);
    }
}