using System.IO;
using System.Text;
using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy;
public class MarkdownExportStrategy : IExportStrategy
{
    public string ExportName => "Markdown";
    public string FileExtension => ".md";

    public string Export(JsonSchemaDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# JSON Schema Documentation");
        sb.AppendLine();
        sb.AppendLine($"**File:** `{Path.GetFileName(document.FilePath)}`");
        sb.AppendLine($"**Last Modified:** {document.LastModified:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## Properties");
        sb.AppendLine();

        ExportProperties(sb, document.Properties, 0);

        return sb.ToString();
    }

    private void ExportProperties(StringBuilder sb, List<JsonPropertyMetadata> properties, int depth)
    {
        foreach (var prop in properties)
        {
            var indent = new string('#', Math.Min(depth + 3, 6));
            sb.AppendLine($"{indent} {prop.Name}");
            sb.AppendLine();

            sb.AppendLine("| Attribute | Value |");
            sb.AppendLine("|-----------|-------|");
            sb.AppendLine($"| **Type** | `{prop.DataType}` |");
            
            if (!string.IsNullOrEmpty(prop.Format))
                sb.AppendLine($"| **Format** | `{prop.Format}` |");
            
            if (!string.IsNullOrEmpty(prop.Description))
                sb.AppendLine($"| **Description** | {prop.Description} |");
            
            if (!string.IsNullOrEmpty(prop.Example))
                sb.AppendLine($"| **Example** | `{prop.Example}` |");
            
            sb.AppendLine($"| **Required** | {(prop.IsRequired ? "Yes" : "No")} |");
            sb.AppendLine($"| **Path** | `{prop.Path}` |");
            sb.AppendLine();

            if (prop.Children.Count > 0)
            {
                ExportProperties(sb, prop.Children, depth + 1);
            }
        }
    }

    public async Task ExportToFileAsync(JsonSchemaDocument document, string filePath)
    {
        var content = Export(document);
        await File.WriteAllTextAsync(filePath, content);
    }
}