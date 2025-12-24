using Newtonsoft.Json.Linq;
using JsonTool.Core.Models;

namespace JsonTool.Core.TemplateMethod;
public class SchemaProcessor : JsonProcessorBase
{
    public override string ProcessorName => "Schema Processor";
    public override string Description => "Processes JSON Schema and extracts property metadata";
    public bool IncludeNestedProperties { get; set; } = true;
    public int MaxDepth { get; set; } = 10;
    public event EventHandler<PropertyFoundEventArgs>? PropertyFound;

    protected override bool ValidateInput(string jsonContent, ProcessingResult result)
    {
        if (!base.ValidateInput(jsonContent, result))
        {
            return false;
        }

        try
        {
            var json = JObject.Parse(jsonContent);
            if (!json.ContainsKey("type") && !json.ContainsKey("properties") && 
                !json.ContainsKey("$ref") && !json.ContainsKey("allOf"))
            {
                result.ErrorMessage = "Document does not appear to be a valid JSON Schema";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to parse as JSON Schema: {ex.Message}";
            return false;
        }
    }

    protected override object ParseJson(string jsonContent)
    {
        var schema = JObject.Parse(jsonContent);
        var schemaInfo = new SchemaInfo
        {
            Title = schema["title"]?.ToString(),
            Description = schema["description"]?.ToString(),
            SchemaVersion = schema["$schema"]?.ToString(),
            Type = schema["type"]?.ToString() ?? "object"
        };
        if (schema["properties"] is JObject properties)
        {
            var required = schema["required"] as JArray;
            var requiredSet = required?.Select(r => r.ToString()).ToHashSet() ?? new HashSet<string>();

            foreach (var prop in properties.Properties())
            {
                var propInfo = ParseProperty(prop, "$", requiredSet, 0);
                schemaInfo.Properties.Add(propInfo);
                
                PropertyFound?.Invoke(this, new PropertyFoundEventArgs { Property = propInfo });
            }
        }

        return schemaInfo;
    }

    private SchemaPropertyInfo ParseProperty(JProperty prop, string parentPath, HashSet<string> requiredProps, int depth)
    {
        var path = $"{parentPath}.{prop.Name}";
        var value = prop.Value as JObject ?? new JObject();

        var propInfo = new SchemaPropertyInfo
        {
            Name = prop.Name,
            Path = path,
            Type = value["type"]?.ToString() ?? "unknown",
            Description = value["description"]?.ToString(),
            Format = value["format"]?.ToString(),
            Example = value["example"]?.ToString() ?? value["default"]?.ToString(),
            IsRequired = requiredProps.Contains(prop.Name),
            Depth = depth,
            Minimum = value["minimum"]?.ToObject<decimal?>(),
            Maximum = value["maximum"]?.ToObject<decimal?>(),
            MinLength = value["minLength"]?.ToObject<int?>(),
            MaxLength = value["maxLength"]?.ToObject<int?>(),
            Pattern = value["pattern"]?.ToString(),
            EnumValues = value["enum"]?.ToObject<List<string>>()
        };
        if (IncludeNestedProperties && depth < MaxDepth)
        {
            if (value["properties"] is JObject nestedProps)
            {
                var nestedRequired = value["required"] as JArray;
                var nestedRequiredSet = nestedRequired?.Select(r => r.ToString()).ToHashSet() ?? new HashSet<string>();

                foreach (var nestedProp in nestedProps.Properties())
                {
                    var childInfo = ParseProperty(nestedProp, path, nestedRequiredSet, depth + 1);
                    propInfo.Children.Add(childInfo);
                    
                    PropertyFound?.Invoke(this, new PropertyFoundEventArgs { Property = childInfo });
                }
            }
            if (propInfo.Type == "array" && value["items"] is JObject items)
            {
                propInfo.ArrayItemType = items["type"]?.ToString();
                
                if (items["properties"] is JObject itemProps)
                {
                    var itemRequired = items["required"] as JArray;
                    var itemRequiredSet = itemRequired?.Select(r => r.ToString()).ToHashSet() ?? new HashSet<string>();

                    foreach (var itemProp in itemProps.Properties())
                    {
                        var childInfo = ParseProperty(itemProp, $"{path}[*]", itemRequiredSet, depth + 1);
                        propInfo.Children.Add(childInfo);
                        
                        PropertyFound?.Invoke(this, new PropertyFoundEventArgs { Property = childInfo });
                    }
                }
            }
        }

        return propInfo;
    }

    protected override object TransformData(object parsedData)
    {
        if (parsedData is not SchemaInfo schemaInfo)
        {
            throw new InvalidOperationException("Expected SchemaInfo from ParseJson");
        }
        schemaInfo.Statistics = CalculateStatistics(schemaInfo.Properties);

        return schemaInfo;
    }

    private SchemaStatistics CalculateStatistics(List<SchemaPropertyInfo> properties)
    {
        var stats = new SchemaStatistics();
        CalculateStatsRecursive(properties, stats);
        return stats;
    }

    private void CalculateStatsRecursive(List<SchemaPropertyInfo> properties, SchemaStatistics stats)
    {
        foreach (var prop in properties)
        {
            stats.TotalProperties++;
            
            if (prop.IsRequired)
                stats.RequiredProperties++;

            if (!stats.TypeCounts.ContainsKey(prop.Type))
                stats.TypeCounts[prop.Type] = 0;
            stats.TypeCounts[prop.Type]++;

            if (prop.Depth > stats.MaxDepth)
                stats.MaxDepth = prop.Depth;

            if (prop.Children.Count > 0)
            {
                CalculateStatsRecursive(prop.Children, stats);
            }
        }
    }

    protected override string FormatOutput(object transformedData)
    {
        if (transformedData is not SchemaInfo schemaInfo)
        {
            return base.FormatOutput(transformedData);
        }

        var lines = new List<string>
        {
            $"Schema: {schemaInfo.Title ?? "Untitled"}",
            $"Type: {schemaInfo.Type}",
            $"Description: {schemaInfo.Description ?? "N/A"}",
            "",
            "Properties:",
            new string('-', 50)
        };

        FormatPropertiesRecursive(schemaInfo.Properties, lines, 0);

        lines.Add("");
        lines.Add("Statistics:");
        lines.Add($"  Total Properties: {schemaInfo.Statistics.TotalProperties}");
        lines.Add($"  Required: {schemaInfo.Statistics.RequiredProperties}");
        lines.Add($"  Max Depth: {schemaInfo.Statistics.MaxDepth}");
        lines.Add($"  Types: {string.Join(", ", schemaInfo.Statistics.TypeCounts.Select(kv => $"{kv.Key}({kv.Value})"))}");

        return string.Join(Environment.NewLine, lines);
    }

    private void FormatPropertiesRecursive(List<SchemaPropertyInfo> properties, List<string> lines, int indent)
    {
        var prefix = new string(' ', indent * 2);
        
        foreach (var prop in properties)
        {
            var required = prop.IsRequired ? " *" : "";
            var format = !string.IsNullOrEmpty(prop.Format) ? $" ({prop.Format})" : "";
            
            lines.Add($"{prefix}- {prop.Name}: {prop.Type}{format}{required}");
            
            if (!string.IsNullOrEmpty(prop.Description))
            {
                lines.Add($"{prefix}  Description: {prop.Description}");
            }

            if (prop.Children.Count > 0)
            {
                FormatPropertiesRecursive(prop.Children, lines, indent + 1);
            }
        }
    }

    protected override void OnAfterProcessing(ProcessingResult result)
    {
        if (result.TransformedData is SchemaInfo schemaInfo)
        {
            result.Metadata["PropertyCount"] = schemaInfo.Statistics.TotalProperties;
            result.Metadata["RequiredCount"] = schemaInfo.Statistics.RequiredProperties;
            result.Metadata["MaxDepth"] = schemaInfo.Statistics.MaxDepth;
        }
    }
}

#region Data Models
public class SchemaInfo
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? SchemaVersion { get; set; }
    public string Type { get; set; } = "object";
    public List<SchemaPropertyInfo> Properties { get; } = new();
    public SchemaStatistics Statistics { get; set; } = new();
}
public class SchemaPropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "unknown";
    public string? Description { get; set; }
    public string? Format { get; set; }
    public string? Example { get; set; }
    public bool IsRequired { get; set; }
    public int Depth { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public List<string>? EnumValues { get; set; }
    public string? ArrayItemType { get; set; }
    public List<SchemaPropertyInfo> Children { get; } = new();
}
public class SchemaStatistics
{
    public int TotalProperties { get; set; }
    public int RequiredProperties { get; set; }
    public int MaxDepth { get; set; }
    public Dictionary<string, int> TypeCounts { get; } = new();
}
public class PropertyFoundEventArgs : EventArgs
{
    public SchemaPropertyInfo Property { get; set; } = null!;
}

#endregion