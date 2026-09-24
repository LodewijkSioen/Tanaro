using Tanaro.DemoFunction;

namespace Tanaro.Nunit.Tests;

public class CosmosDbTriggerTests
{
    [Test]
    public async Task ListShapeReturnsCount()
    {
        var documents = new List<CosmosDocument> { new("1", "a") };

        var result = await AppUnderTest.Host.Run<CosmosFunction>().CosmosList(s => s.Execute(documents));
        Assert.That(result.Value, Is.EqualTo("1"));
    }

    [Test]
    public async Task ArrayShapeReturnsCount()
    {
        var documents = new[] { new CosmosDocument("1", "a") };

        var result = await AppUnderTest.Host.Run<CosmosFunction>().CosmosArray(s => s.Execute(documents));
        Assert.That(result.Value, Is.EqualTo("1"));
    }

    [Test]
    public async Task FunctionDefinitionInputBindingsAreExtractedFromTriggerAttribute()
    {
        var documents = new List<CosmosDocument> { new("1", "a") };

        var result = await AppUnderTest.Host.Run<CosmosFunction>().CosmosList(s => s.Execute(documents));

        var binding = result.FunctionContext.FunctionDefinition.InputBindings["documents"];
        Assert.That(binding.Type, Is.EqualTo("cosmosDBTrigger"));
    }
}
