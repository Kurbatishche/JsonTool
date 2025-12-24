using System.Text;
using Newtonsoft.Json.Linq;

namespace JsonTool.Core.TemplateMethod;
public class MarkdownExporter : JsonProcessorBase
{
    public override string ProcessorName => "Markdown Exporter";
    public override string Description => "Exports JSON Schema to Markdown documentation";
    public string? DocumentTitle { get; set; }
    public bool IncludeTableOfContents { get; set; } = true;
    public bool IncludeExamples { get; set; } = true;
    public bool IncludeJsonExample { get; set; } = true;
    public TableStyle TableStyle { get; set; } = TableStyle.GitHub;
    public int MaxHeadingLevel { get; set; } = 4;

    protected override bool ValidateInput(string jsonContent, ProcessingResult result)
    {
        if (!base.ValidateInput(jsonContent, result))
        {
            return false;
        }

        try
        {
            var json = JObject.Parse(jsonContent);
            if (!json.ContainsKey("type") && !json.ContainsKey("properties"))
            {
                result.ErrorMessage = "Document does not appear to be a valid JSON Schema";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Invalid JSON Schema: {ex.Message}";
            return false;
        }
    }

    protected override object ParseJson(string jsonContent)
    {
        var schema = JObject.Parse(jsonContent);
        
        return new MarkdownSchemaInfo
        {
            RawSchema = schema,
            Title = schema["title"]?.ToString() ?? DocumentTitle ?? "JSON Schema",
            Description = schema["description"]?.ToString(),
            SchemaVersion = schema["$schema"]?.ToString(),
            Type = schema["type"]?.ToString() ?? "object",
            Properties = ParseProperties(schema),
            Required = (schema["required"] as JArray)?.Select(r => r.ToString()).ToList() ?? new List<string>()
        };
    }

    private List<MarkdownPropertyInfo> ParseProperties(JObject schema, string parentPath = "", int depth = 0)
    {
        var properties = new List<MarkdownPropertyInfo>();
        var propsToken = schema["properties"] as JObject;
        
        if (propsToken == null) return properties;

        var required = (schema["required"] as JArray)?.Select(r => r.ToString()).ToHashSet() 
            ?? new HashSet<string>();

        foreach (var prop in propsToken.Properties())
        {
            var propDef = prop.Value as JObject ?? new JObject();
            var path = string.IsNullOrEmpty(parentPath) ? prop.Name : $"{parentPath}.{prop.Name}";

            var propInfo = new MarkdownPropertyInfo
            {
                Name = prop.Name,
                Path = path,
                Type = propDef["type"]?.ToString() ?? "any",
                Description = propDef["description"]?.ToString(),
                Format = propDef["format"]?.ToString(),
                Example = propDef["example"]?.ToString() ?? propDef["default"]?.ToString(),
                Default = propDef["default"]?.ToString(),
                IsRequired = required.Contains(prop.Name),
                Depth = depth,
                Constraints = ExtractConstraints(propDef),
                EnumValues = (propDef["enum"] as JArray)?.Select(e => e.ToString()).ToList()
            };
            if (propDef["properties"] != null)
            {
                propInfo.Children = ParseProperties(propDef, path, depth + 1);
            }
            if (propInfo.Type == "array" && propDef["items"] is JObject items)
            {
                propInfo.ArrayItemType = items["type"]?.ToString();
                if (items["properties"] != null)
                {
                    propInfo.Children = ParseProperties(items, $"{path}[]", depth + 1);
                }
            }

            properties.Add(propInfo);
        }

        return properties;
    }

    private Dictionary<string, string> ExtractConstraints(JObject propDef)
    {
        var constraints = new Dictionary<string, string>();

        var constraintKeys = new[] 
        { 
            "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum",
            "minLength", "maxLength", "pattern", "minItems", "maxItems",
            "uniqueItems", "minProperties", "maxProperties"
        };

        foreach (var key in constraintKeys)
        {
            if (propDef[key] != null)
            {
                constraints[key] = propDef[key]!.ToString();
            }
        }

        return constraints;
    }

    protected override object TransformData(object parsedData)
    {
        if (parsedData is not MarkdownSchemaInfo schemaInfo)
        {
            throw new InvalidOperationException("Expected MarkdownSchemaInfo from ParseJson");
        }
        var markdown = GenerateMarkdown(schemaInfo);
        schemaInfo.GeneratedMarkdown = markdown;

        return schemaInfo;
    }

    private string GenerateMarkdown(MarkdownSchemaInfo schema)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {schema.Title}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(schema.Description))
        {
            sb.AppendLine(schema.Description);
            sb.AppendLine();
        }
        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine($"- **Type:** `{schema.Type}`");
        if (!string.IsNullOrEmpty(schema.SchemaVersion))
        {
            sb.AppendLine($"- **Schema Version:** `{schema.SchemaVersion}`");
        }
        sb.AppendLine($"- **Properties:** {CountAllProperties(schema.Properties)}");
        sb.AppendLine($"- **Required:** {schema.Required.Count}");
        sb.AppendLine();
        if (IncludeTableOfContents && schema.Properties.Count > 0)
        {
            sb.AppendLine("## Table of Contents");
            sb.AppendLine();
            GenerateTableOfContents(sb, schema.Properties, 0);
            sb.AppendLine();
        }
        sb.AppendLine("## Properties");
        sb.AppendLine();
        GeneratePropertiesMarkdown(sb, schema.Properties, schema.Required, 3);
        if (IncludeJsonExample)
        {
            sb.AppendLine("## Example");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(GenerateExampleJson(schema));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void GenerateTableOfContents(StringBuilder sb, List<MarkdownPropertyInfo> properties, int indent)
    {
        var prefix = new string(' ', indent * 2);
        
        foreach (var prop in properties)
        {
            var anchor = prop.Path.Replace(".", "").Replace("[]", "").ToLower();
            sb.AppendLine($"{prefix}- [{prop.Name}](#{anchor})");
            
            if (prop.Children.Count > 0)
            {
                GenerateTableOfContents(sb, prop.Children, indent + 1);
            }
        }
    }

    private void GeneratePropertiesMarkdown(StringBuilder sb, List<MarkdownPropertyInfo> properties, 
        List<string> rootRequired, int headingLevel)
    {
        foreach (var prop in properties)
        {
            var level = Math.Min(headingLevel, MaxHeadingLevel);
            var heading = new string('#', level);
            var required = prop.IsRequired ? " *(required)*" : "";

            sb.AppendLine($"{heading} `{prop.Name}`{required}");
            sb.AppendLine();
            sb.AppendLine("| Attribute | Value |");
            sb.AppendLine("|-----------|-------|");
            sb.AppendLine($"| **Path** | `{prop.Path}` |");
            sb.AppendLine($"| **Type** | `{prop.Type}`{(prop.ArrayItemType != null ? $" of `{prop.ArrayItemType}`" : "")} |");
            
            if (!string.IsNullOrEmpty(prop.Format))
                sb.AppendLine($"| **Format** | `{prop.Format}` |");
            
            if (!string.IsNullOrEmpty(prop.Description))
                sb.AppendLine($"| **Description** | {prop.Description} |");
            
            if (!string.IsNullOrEmpty(prop.Default))
                sb.AppendLine($"| **Default** | `{prop.Default}` |");
            foreach (var constraint in prop.Constraints)
            {
                sb.AppendLine($"| **{FormatConstraintName(constraint.Key)}** | `{constraint.Value}` |");
            }

            sb.AppendLine();
            if (prop.EnumValues?.Count > 0)
            {
                sb.AppendLine("**Allowed values:**");
                sb.AppendLine();
                foreach (var val in prop.EnumValues)
                {
                    sb.AppendLine($"- `{val}`");
                }
                sb.AppendLine();
            }
            if (IncludeExamples && !string.IsNullOrEmpty(prop.Example))
            {
                sb.AppendLine($"**Example:** `{prop.Example}`");
                sb.AppendLine();
            }
            if (prop.Children.Count > 0)
            {
                GeneratePropertiesMarkdown(sb, prop.Children, rootRequired, headingLevel + 1);
            }
        }
    }

    private string GenerateExampleJson(MarkdownSchemaInfo schema)
    {
        var example = new JObject();
        GenerateExampleProperties(example, schema.Properties);
        return example.ToString(Newtonsoft.Json.Formatting.Indented);
    }

    private void GenerateExampleProperties(JObject obj, List<MarkdownPropertyInfo> properties)
    {
        foreach (var prop in properties)
        {
            obj[prop.Name] = GenerateExampleValue(prop);
        }
    }

    private JToken GenerateExampleValue(MarkdownPropertyInfo prop)
    {
        if (!string.IsNullOrEmpty(prop.Example))
        {
            return JToken.Parse($"\"{prop.Example}\"");
        }
        if (prop.EnumValues?.Count > 0)
        {
            return prop.EnumValues[0];
        }
        return prop.Type switch
        {
            "string" => prop.Format switch
            {
                "email" => "user@example.com",
                "uri" => "https://example.com",
                "date" => "2024-01-01",
                "date-time" => "2024-01-01T00:00:00Z",
                "uuid" => "550e8400-e29b-41d4-a716-446655440000",
                _ => "string"
            },
            "integer" => 0,
            "number" => 0.0,
            "boolean" => false,
            "array" => GenerateExampleArray(prop),
            "object" => GenerateExampleObject(prop),
            "null" => JValue.CreateNull(),
            _ => "value"
        };
    }

    private JArray GenerateExampleArray(MarkdownPropertyInfo prop)
    {
        var array = new JArray();
        if (prop.Children.Count > 0)
        {
            var item = new JObject();
            GenerateExampleProperties(item, prop.Children);
            array.Add(item);
        }
        else if (!string.IsNullOrEmpty(prop.ArrayItemType))
        {
            array.Add(GenerateExampleValue(new MarkdownPropertyInfo { Type = prop.ArrayItemType }));
        }
        return array;
    }

    private JObject GenerateExampleObject(MarkdownPropertyInfo prop)
    {
        var obj = new JObject();
        if (prop.Children.Count > 0)
        {
            GenerateExampleProperties(obj, prop.Children);
        }
        return obj;
    }

    private static string FormatConstraintName(string name)
    {
        return name switch
        {
            "minLength" => "Min Length",
            "maxLength" => "Max Length",
            "minimum" => "Minimum",
            "maximum" => "Maximum",
            "exclusiveMinimum" => "Exclusive Min",
            "exclusiveMaximum" => "Exclusive Max",
            "minItems" => "Min Items",
            "maxItems" => "Max Items",
            "uniqueItems" => "Unique Items",
            "pattern" => "Pattern",
            _ => name
        };
    }

    private static int CountAllProperties(List<MarkdownPropertyInfo> properties)
    {
        int count = properties.Count;
        foreach (var prop in properties)
        {
            count += CountAllProperties(prop.Children);
        }
        return count;
    }

    protected override string FormatOutput(object transformedData)
    {
        if (transformedData is MarkdownSchemaInfo schemaInfo)
        {
            return schemaInfo.GeneratedMarkdown;
        }
        return base.FormatOutput(transformedData);
    }

    protected override void OnAfterProcessing(ProcessingResult result)
    {
        if (result.TransformedData is MarkdownSchemaInfo schemaInfo)
        {
            result.Metadata["Title"] = schemaInfo.Title;
            result.Metadata["PropertyCount"] = CountAllProperties(schemaInfo.Properties);
            result.Metadata["MarkdownLength"] = schemaInfo.GeneratedMarkdown.Length;
        }
    }
}

#region Data Models
public class MarkdownSchemaInfo
{
    public JObject RawSchema { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SchemaVersion { get; set; }
    public string Type { get; set; } = "object";
    public List<MarkdownPropertyInfo> Properties { get; set; } = new();
    public List<string> Required { get; set; } = new();
    public string GeneratedMarkdown { get; set; } = string.Empty;
}
public class MarkdownPropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "any";
    public string? Description { get; set; }
    public string? Format { get; set; }
    public string? Example { get; set; }
    public string? Default { get; set; }
    public bool IsRequired { get; set; }
    public int Depth { get; set; }
    public string? ArrayItemType { get; set; }
    public Dictionary<string, string> Constraints { get; set; } = new();
    public List<string>? EnumValues { get; set; }
    public List<MarkdownPropertyInfo> Children { get; set; } = new();
}
public enum TableStyle
{
    GitHub,
    Simple,
    Grid
}

#endregion