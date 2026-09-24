using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

public class CosmosFunction
{
    [Function("CosmosList")]
    public string RunList([CosmosDBTrigger("db", "container", Connection = "CosmosDBConnection")] IReadOnlyList<CosmosDocument> documents) =>
        documents.Count.ToString();

    [Function("CosmosArray")]
    public string RunArray([CosmosDBTrigger("db", "container", Connection = "CosmosDBConnection")] CosmosDocument[] documents) =>
        documents.Length.ToString();
}

public record CosmosDocument(string Id, string Value);
