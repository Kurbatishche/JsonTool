namespace JsonTool.Core.Models;
public class JsonSchemaDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public List<JsonPropertyMetadata> Properties { get; set; } = new();
    public bool IsModified { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}