using System.IO;
using System.Text;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace JsonTool.Core.TemplateMethod;
public class MarkdownTableExporter : JsonProcessorBase
{
    #region Properties

    public override string ProcessorName => "Markdown Table Exporter";
    public override string Description => "Exports JSON Schema as Markdown table with nested object support";
    public string IndentSymbol { get; set; } = "└─ ";
    public string ContinueSymbol { get; set; } = "│  ";
    public bool ShowFullPath { get; set; } = false;
    public bool IncludeHeader { get; set; } = true;
    public bool IncludeStatistics { get; set; } = true;
    public int MaxDescriptionLength { get; set; } = 100;
    public int MaxExampleLength { get; set; } = 50;
    public string? DocumentTitle { get; set; }

    #endregion

    #region Template Method Implementation

    protected override bool ValidateInput(string jsonContent, ProcessingResult result)
    {
        if (!base.ValidateInput(jsonContent, result))
            return false;

        try
        {
            var json = JObject.Parse(jsonContent);
            if (!json.ContainsKey("properties") && !json.ContainsKey("type"))
            {
                result.ErrorMessage = "Document does not appear to be a valid JSON Schema (missing 'properties' or 'type')";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Invalid JSON: {ex.Message}";
            return false;
        }
    }

    protected override object ParseJson(string jsonContent)
    {
        var schema = JObject.Parse(jsonContent);
        var tableData = new MarkdownTableData
        {
            Title = schema["title"]?.ToString() ?? DocumentTitle ?? "JSON Schema",
            Description = schema["description"]?.ToString(),
            SchemaVersion = schema["$schema"]?.ToString(),
            SchemaId = schema["$id"]?.ToString()
        };
        var required = (schema["required"] as JArray)?
            .Select(r => r.ToString())
            .ToHashSet() ?? new HashSet<string>();
        ParseProperties(schema, tableData.Rows, required, 0, "");

        return tableData;
    }

    private void ParseProperties(JObject schema, List<TableRow> rows, HashSet<string> required, int depth, string parentPath)
    {
        var properties = schema["properties"] as JObject;
        if (properties == null) return;

        var propList = properties.Properties().ToList();
        
        for (int i = 0; i < propList.Count; i++)
        {
            var prop = propList[i];
            var propDef = prop.Value as JObject ?? new JObject();
            var isLast = i == propList.Count - 1;
            var path = string.IsNullOrEmpty(parentPath) ? prop.Name : $"{parentPath}.{prop.Name}";

            var row = new TableRow
            {
                Name = prop.Name,
                Path = path,
                Type = GetTypeString(propDef),
                IsRequired = required.Contains(prop.Name),
                Description = propDef["description"]?.ToString(),
                Example = GetExample(propDef),
                Format = propDef["format"]?.ToString(),
                Depth = depth,
                IsLastInGroup = isLast
            };

            rows.Add(row);
            if (propDef["properties"] != null)
            {
                var nestedRequired = (propDef["required"] as JArray)?
                    .Select(r => r.ToString())
                    .ToHashSet() ?? new HashSet<string>();

                ParseProperties(propDef, rows, nestedRequired, depth + 1, path);
            }
            if (propDef["type"]?.ToString() == "array" && propDef["items"] is JObject items)
            {
                if (items["properties"] != null)
                {
                    var itemRequired = (items["required"] as JArray)?
                        .Select(r => r.ToString())
                        .ToHashSet() ?? new HashSet<string>();

                    ParseProperties(items, rows, itemRequired, depth + 1, $"{path}[]");
                }
            }
        }
    }

    private string GetTypeString(JObject propDef)
    {
        var type = propDef["type"]?.ToString() ?? "any";
        if (type == "array" && propDef["items"] is JObject items)
        {
            var itemType = items["type"]?.ToString() ?? "any";
            return $"array<{itemType}>";
        }
        if (propDef["enum"] is JArray enumValues)
        {
            var values = enumValues.Select(e => e.ToString()).Take(3);
            var suffix = enumValues.Count > 3 ? ", ..." : "";
            return $"enum({string.Join("|", values)}{suffix})";
        }

        return type;
    }

    private string? GetExample(JObject propDef)
    {
        if (propDef["example"] != null)
            return propDef["example"]?.ToString();
        if (propDef["default"] != null)
            return propDef["default"]?.ToString();
        var format = propDef["format"]?.ToString();
        if (!string.IsNullOrEmpty(format))
        {
            return format switch
            {
                "email" => "user@example.com",
                "uri" => "https://example.com",
                "date" => "2024-01-01",
                "date-time" => "2024-01-01T00:00:00Z",
                "time" => "12:00:00",
                "uuid" => "550e8400-e29b-...",
                "hostname" => "example.com",
                "ipv4" => "192.168.1.1",
                "ipv6" => "::1",
                _ => null
            };
        }

        return null;
    }

    protected override object TransformData(object parsedData)
    {
        if (parsedData is not MarkdownTableData tableData)
            throw new InvalidOperationException("Expected MarkdownTableData");
        tableData.GeneratedMarkdown = GenerateMarkdownTable(tableData);

        return tableData;
    }

    private string GenerateMarkdownTable(MarkdownTableData data)
    {
        var sb = new StringBuilder();
        if (IncludeHeader)
        {
            sb.AppendLine($"# {data.Title}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(data.Description))
            {
                sb.AppendLine(data.Description);
                sb.AppendLine();
            }
        }
        if (IncludeStatistics)
        {
            var totalProps = data.Rows.Count;
            var requiredProps = data.Rows.Count(r => r.IsRequired);
            var maxDepth = data.Rows.Any() ? data.Rows.Max(r => r.Depth) : 0;

            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Value |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Total Properties | {totalProps} |");
            sb.AppendLine($"| Required Properties | {requiredProps} |");
            sb.AppendLine($"| Max Nesting Depth | {maxDepth} |");
            sb.AppendLine();
        }
        sb.AppendLine("## Properties");
        sb.AppendLine();
        sb.AppendLine("| Property Name | Type | Required | Description | Example | Format |");
        sb.AppendLine("|---------------|------|----------|-------------|---------|--------|");
        foreach (var row in data.Rows)
        {
            var name = FormatPropertyName(row);
            var type = EscapeMarkdown(row.Type);
            var required = row.IsRequired ? "✓" : "";
            var description = TruncateAndEscape(row.Description, MaxDescriptionLength);
            var example = TruncateAndEscape(row.Example, MaxExampleLength);
            var format = EscapeMarkdown(row.Format ?? "");

            sb.AppendLine($"| {name} | `{type}` | {required} | {description} | {example} | {format} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Legend: ✓ = Required property*");
        sb.AppendLine();

        return sb.ToString();
    }

    private string FormatPropertyName(TableRow row)
    {
        if (ShowFullPath)
        {
            return $"`{row.Path}`";
        }

        if (row.Depth == 0)
        {
            return $"**{row.Name}**";
        }
        var indent = new StringBuilder();
        for (int i = 0; i < row.Depth - 1; i++)
        {
            indent.Append("&nbsp;&nbsp;&nbsp;&nbsp;");
        }
        indent.Append(IndentSymbol);

        return $"{indent}{row.Name}";
    }

    private string TruncateAndEscape(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var escaped = EscapeMarkdown(text);

        if (maxLength > 0 && escaped.Length > maxLength)
        {
            return escaped.Substring(0, maxLength - 3) + "...";
        }

        return escaped;
    }

    private string EscapeMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("|", "\\|")
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    protected override string FormatOutput(object transformedData)
    {
        if (transformedData is MarkdownTableData tableData)
        {
            return tableData.GeneratedMarkdown;
        }
        return base.FormatOutput(transformedData);
    }

    protected override void OnAfterProcessing(ProcessingResult result)
    {
        if (result.TransformedData is MarkdownTableData tableData)
        {
            result.Metadata["Title"] = tableData.Title;
            result.Metadata["TotalProperties"] = tableData.Rows.Count;
            result.Metadata["RequiredProperties"] = tableData.Rows.Count(r => r.IsRequired);
            result.Metadata["MaxDepth"] = tableData.Rows.Any() ? tableData.Rows.Max(r => r.Depth) : 0;
            result.Metadata["MarkdownLength"] = tableData.GeneratedMarkdown.Length;
        }
    }

    #endregion

    #region Public Methods
    public async Task<ExportResult> ExportToFileAsync(string jsonSchema, string filePath)
    {
        var result = new ExportResult();

        try
        {
            var processingResult = ProcessJson(jsonSchema);
            
            if (!processingResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = processingResult.ErrorMessage;
                return result;
            }

            await File.WriteAllTextAsync(filePath, processingResult.Output, Encoding.UTF8);
            
            result.Success = true;
            result.FilePath = filePath;
            result.Markdown = processingResult.Output;
            result.BytesWritten = Encoding.UTF8.GetByteCount(processingResult.Output);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to save file: {ex.Message}";
        }

        return result;
    }
    public ExportResult ExportToClipboard(string jsonSchema)
    {
        var result = new ExportResult();

        try
        {
            var processingResult = ProcessJson(jsonSchema);
            
            if (!processingResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = processingResult.ErrorMessage;
                return result;
            }
            Clipboard.SetText(processingResult.Output);
            
            result.Success = true;
            result.Markdown = processingResult.Output;
            result.CopiedToClipboard = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to copy to clipboard: {ex.Message}";
        }

        return result;
    }
    public ExportResult Export(string jsonSchema)
    {
        var result = new ExportResult();

        try
        {
            var processingResult = ProcessJson(jsonSchema);
            
            if (!processingResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = processingResult.ErrorMessage;
                return result;
            }

            result.Success = true;
            result.Markdown = processingResult.Output;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Export failed: {ex.Message}";
        }

        return result;
    }
    public string GenerateTableOnly(string jsonSchema)
    {
        var originalIncludeHeader = IncludeHeader;
        var originalIncludeStats = IncludeStatistics;

        try
        {
            IncludeHeader = false;
            IncludeStatistics = false;

            var result = ProcessJson(jsonSchema);
            return result.Success ? result.Output : string.Empty;
        }
        finally
        {
            IncludeHeader = originalIncludeHeader;
            IncludeStatistics = originalIncludeStats;
        }
    }

    #endregion
}

#region Data Models
public class MarkdownTableData
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SchemaVersion { get; set; }
    public string? SchemaId { get; set; }
    public List<TableRow> Rows { get; set; } = new();
    public string GeneratedMarkdown { get; set; } = string.Empty;
}
public class TableRow
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
    public string? Example { get; set; }
    public string? Format { get; set; }
    public int Depth { get; set; }
    public bool IsLastInGroup { get; set; }
}
public class ExportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Markdown { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public bool CopiedToClipboard { get; set; }
    public long BytesWritten { get; set; }
}

#endregion