using JsonTool.Core.Models;

namespace JsonTool.Services;
public interface IJsonSchemaService
{
    Task<JsonSchemaDocument> LoadSchemaAsync(string filePath);
    Task SaveSchemaAsync(JsonSchemaDocument document);
    Task<ValidationResult> ValidateSchemaAsync(string content);
    Task<ValidationResult> ValidateDataAgainstSchemaAsync(string data, string schema);
    List<JsonPropertyMetadata> ParseSchemaProperties(string content);
    string UpdateSchemaWithMetadata(string content, JsonPropertyMetadata property);
}