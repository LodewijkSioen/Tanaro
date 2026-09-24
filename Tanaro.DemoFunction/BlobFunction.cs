using System.Text;
using Microsoft.Azure.Functions.Worker;

namespace Tanaro.DemoFunction;

public class BlobFunction
{
    [Function("BlobString")]
    public string RunString([BlobTrigger("container/{name}")] string content) => content;

    [Function("BlobBytes")]
    public string RunBytes([BlobTrigger("container/{name}")] byte[] content) => Encoding.UTF8.GetString(content);

    [Function("BlobStream")]
    public string RunStream([BlobTrigger("container/{name}")] Stream content)
    {
        using var reader = new StreamReader(content);
        return reader.ReadToEnd();
    }

    [Function("BlobPoco")]
    public string RunPoco([BlobTrigger("container/{name}")] BlobPayload payload) => payload.Name;
}

public record BlobPayload(string Name);
