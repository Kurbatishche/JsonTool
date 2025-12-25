using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public class TreeViewStrategy : IJsonViewStrategy
{
    public string ViewName => "Tree View";

    public List<JsonPropertyMetadata> TransformForView(JsonSchemaDocument document)
    {
        return document.Properties;
    }
}