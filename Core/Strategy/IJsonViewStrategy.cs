using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public interface IJsonViewStrategy
{
    string ViewName { get; }
    List<JsonPropertyMetadata> TransformForView(JsonSchemaDocument document);
}