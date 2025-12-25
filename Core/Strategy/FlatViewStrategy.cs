using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public class FlatViewStrategy : IJsonViewStrategy
{
    public string ViewName => "Flat View";

    public List<JsonPropertyMetadata> TransformForView(JsonSchemaDocument document)
    {
        var flatList = new List<JsonPropertyMetadata>();
        FlattenProperties(document.Properties, flatList);
        return flatList;
    }

    private void FlattenProperties(List<JsonPropertyMetadata> properties, List<JsonPropertyMetadata> result)
    {
        foreach (var prop in properties)
        {
            var flatProp = new JsonPropertyMetadata
            {
                Name = prop.Name,
                Description = prop.Description,
                Example = prop.Example,
                DataType = prop.DataType,
                Format = prop.Format,
                IsRequired = prop.IsRequired,
                Path = prop.Path,
                Children = new List<JsonPropertyMetadata>() // Порожній список для flat view
            };
            result.Add(flatProp);
            if (prop.Children.Count > 0)
            {
                FlattenProperties(prop.Children, result);
            }
        }
    }
}