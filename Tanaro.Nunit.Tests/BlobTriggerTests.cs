using System.Text;
using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class BlobTriggerTests
{
    [Test]
    public async Task StringShapeReturnsContent()
    {
        var result = await AppUnderTest.Host.Run<BlobFunction>().BlobString(s => s.Execute("hello"));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task BytesShapeReturnsDecodedContent()
    {
        var result = await AppUnderTest.Host.Run<BlobFunction>()
            .BlobBytes(s => s.Execute(Encoding.UTF8.GetBytes("hello")));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task StreamShapeReturnsContent()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var result = await AppUnderTest.Host.Run<BlobFunction>().BlobStream(s => s.Execute(stream));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task PocoShapeReturnsName()
    {
        var result = await AppUnderTest.Host.Run<BlobFunction>().BlobPoco(s => s.Execute(new BlobPayload("hello")));
        Assert.That(result.Value, Is.EqualTo("hello"));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var result = await AppUnderTest.Host.Run<BlobFunction>().BlobString(s => s.Execute("hello"));

        var binding = result.FunctionContext.FunctionDefinition.InputBindings["content"];
        Assert.That(binding.Type, Is.EqualTo("blobTrigger"));
    }
}
