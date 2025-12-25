using System.IO;
using JsonTool.Core.Models;
using Newtonsoft.Json;

namespace JsonTool.Core.Strategy;
public class JsonExportStrategy : IExportStrategy
{
    public string ExportName => "JSON";
    public string FileExtension => ".json";

    public string Export(JsonSchemaDocument document)
    {
        return document.RawContent;
    }

    public async Task ExportToFileAsync(JsonSchemaDocument document, string filePath)
    {
        await File.WriteAllTextAsync(filePath, document.RawContent);
    }
}