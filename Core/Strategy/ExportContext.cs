using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public class ExportContext
{
    private IExportStrategy _strategy;

    public ExportContext(IExportStrategy strategy)
    {
        _strategy = strategy;
    }

    public void SetStrategy(IExportStrategy strategy)
    {
        _strategy = strategy;
    }

    public string Export(JsonSchemaDocument document)
    {
        return _strategy.Export(document);
    }

    public async Task ExportToFileAsync(JsonSchemaDocument document, string filePath)
    {
        await _strategy.ExportToFileAsync(document, filePath);
    }

    public string GetFileExtension() => _strategy.FileExtension;
    public string GetExportName() => _strategy.ExportName;
}