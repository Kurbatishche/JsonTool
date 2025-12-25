using System.IO;
using JsonTool.Core.Models;
using JsonTool.Core.TemplateMethod;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonTool.Services;
public class JsonSchemaService : IJsonSchemaService
{
    public async Task<JsonSchemaDocument> LoadSchemaAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        
        var document = new JsonSchemaDocument
        {
            FilePath = filePath,
            RawContent = content,
            LastModified = File.GetLastWriteTime(filePath),
            IsModified = false
        };

        try
        {
            document.Properties = ParseSchemaProperties(content);
            var validationResult = await ValidateSchemaAsync(content);
            document.IsValid = validationResult.IsValid;
            document.ValidationErrors = validationResult.Errors.Select(e => e.Message).ToList();
        }
        catch (Exception ex)
        {
            document.IsValid = false;
            document.ValidationErrors.Add(ex.Message);
        }

        return document;
    }

    public async Task SaveSchemaAsync(JsonSchemaDocument document)
    {
        await File.WriteAllTextAsync(document.FilePath, document.RawContent);
        document.IsModified = false;
        document.LastModified = DateTime.Now;
    }

    public Task<ValidationResult> ValidateSchemaAsync(string content)
    {
        var validator = new JsonSchemaValidator();
        var result = validator.Validate(content);
        return Task.FromResult(result);
    }

    public Task<ValidationResult> ValidateDataAgainstSchemaAsync(string data, string schema)
    {
        var validator = new JsonDataValidator(schema);
        var result = validator.Validate(data);
        return Task.FromResult(result);
    }

    public List<JsonPropertyMetadata> ParseSchemaProperties(string content)
    {
        var properties = new List<JsonPropertyMetadata>();

        try
        {
            var json = JObject.Parse(content);
            var propsToken = json["properties"];
            var requiredArray = json["required"] as JArray;
            var requiredProps = requiredArray?.Select(t => t.ToString()).ToHashSet() ?? new HashSet<string>();

            if (propsToken is JObject propsObject)
            {
                foreach (var prop in propsObject.Properties())
                {
                    var metadata = ParseProperty(prop, "$", requiredProps);
                    properties.Add(metadata);
                }
            }
        }
        catch
        {
        }

        return properties;
    }

    private JsonPropertyMetadata ParseProperty(JProperty prop, string parentPath, HashSet<string> requiredProps)
    {
        var path = $"{parentPath}.{prop.Name}";
        var value = prop.Value as JObject ?? new JObject();
        var exampleValue = string.Empty;
        if (value["examples"] is JArray examplesArray && examplesArray.Count > 0)
        {
            exampleValue = string.Join(", ", examplesArray.Select(e => e.ToString()));
        }
        else if (value["example"] != null)
        {
            exampleValue = value["example"].ToString();
        }
        else if (value["default"] != null)
        {
            exampleValue = value["default"].ToString();
        }

        var metadata = new JsonPropertyMetadata
        {
            Name = prop.Name,
            Path = path,
            DataType = value["type"]?.ToString() ?? "unknown",
            Description = value["description"]?.ToString() ?? string.Empty,
            Example = exampleValue,
            Format = value["format"]?.ToString() ?? string.Empty,
            IsRequired = requiredProps.Contains(prop.Name)
        };
        var nestedProps = value["properties"];
        if (nestedProps is JObject nestedObject)
        {
            var nestedRequired = value["required"] as JArray;
            var nestedRequiredProps = nestedRequired?.Select(t => t.ToString()).ToHashSet() ?? new HashSet<string>();

            foreach (var nestedProp in nestedObject.Properties())
            {
                var child = ParseProperty(nestedProp, path, nestedRequiredProps);
                metadata.Children.Add(child);
            }
        }
        if (metadata.DataType == "array" && value["items"] is JObject itemsObject)
        {
            var itemsProps = itemsObject["properties"];
            if (itemsProps is JObject itemsPropsObject)
            {
                var itemsRequired = itemsObject["required"] as JArray;
                var itemsRequiredProps = itemsRequired?.Select(t => t.ToString()).ToHashSet() ?? new HashSet<string>();

                foreach (var itemProp in itemsPropsObject.Properties())
                {
                    var child = ParseProperty(itemProp, $"{path}[*]", itemsRequiredProps);
                    metadata.Children.Add(child);
                }
            }
        }

        return metadata;
    }

    public string UpdateSchemaWithMetadata(string content, JsonPropertyMetadata property)
    {
        try
        {
            var json = JObject.Parse(content);
            var pathParts = property.Path.Split('.').Skip(1).ToArray();
            
            JToken? current = json;
            foreach (var part in pathParts.SkipLast(1))
            {
                if (part.EndsWith("[*]"))
                {
                    var propName = part.Replace("[*]", "");
                    current = current?[propName]?["items"]?["properties"];
                }
                else
                {
                    current = current?["properties"]?[part];
                }
            }

            var propName2 = pathParts.LastOrDefault()?.Replace("[*]", "") ?? property.Name;
            var targetProp = current?["properties"]?[propName2] as JObject;

            if (targetProp != null)
            {
                targetProp["description"] = property.Description;
                targetProp["type"] = property.DataType;
                
                if (!string.IsNullOrEmpty(property.Format))
                    targetProp["format"] = property.Format;
                else
                    targetProp.Remove("format");
                if (!string.IsNullOrEmpty(property.Example))
                {
                    var examples = property.Example
                        .Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrEmpty(e))
                        .ToArray();
                    
                    if (examples.Length > 0)
                    {
                        targetProp["examples"] = new JArray(examples);
                        targetProp.Remove("example");
                    }
                }
                else
                {
                    targetProp.Remove("examples");
                    targetProp.Remove("example");
                }
            }

            return json.ToString(Formatting.Indented);
        }
        catch
        {
            return content;
        }
    }
}