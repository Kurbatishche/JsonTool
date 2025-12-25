using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public interface IExportStrategy
{
    string ExportName { get; }
    string FileExtension { get; }
    string Export(JsonSchemaDocument document);
    Task ExportToFileAsync(JsonSchemaDocument document, string filePath);
}