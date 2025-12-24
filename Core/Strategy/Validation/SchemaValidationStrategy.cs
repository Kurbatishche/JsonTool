using JsonTool.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace JsonTool.Core.Strategy.Validation;
public class SchemaValidationStrategy : IValidationStrategy
{
    public string StrategyName => "Schema Validation";
    public string Description => "Validates that the document is a valid JSON Schema";

    public bool CanValidate(string content, string? schema = null)
    {
        return !string.IsNullOrEmpty(content);
    }

    public ValidationResult Validate(string content, string? schema = null)
    {
        return ValidateAsync(content, schema).GetAwaiter().GetResult();
    }

    public async Task<ValidationResult> ValidateAsync(string content, string? schema = null)
    {
        var result = new ValidationResult { IsValid = true };
        var syntaxStrategy = new SyntaxValidationStrategy();
        var syntaxResult = syntaxStrategy.Validate(content);
        
        if (!syntaxResult.IsValid)
        {
            return syntaxResult;
        }

        try
        {
            var jsonSchema = await JsonSchema.FromJsonAsync(content);
            var jsonObject = JObject.Parse(content);
            
            ValidateSchemaStructure(jsonObject, result);
            ValidateSchemaProperties(jsonSchema, jsonObject, result);
            ValidateSchemaKeywords(jsonObject, result);
        }
        catch (JsonReaderException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = $"JSON parsing error: {ex.Message}",
                Path = ex.Path ?? "$",
                LineNumber = ex.LineNumber,
                Column = ex.LinePosition,
                ErrorType = ValidationErrorType.Syntax
            });
        }
        catch (JsonException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = $"Invalid JSON Schema: {ex.Message}",
                Path = "$",
                ErrorType = ValidationErrorType.Schema
            });
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = $"Schema validation error: {ex.Message}",
                Path = "$",
                ErrorType = ValidationErrorType.Schema
            });
        }

        return result;
    }

    private void ValidateSchemaStructure(JObject jsonObject, ValidationResult result)
    {
        if (!jsonObject.ContainsKey("$schema"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Missing '$schema' property. Consider adding it to specify JSON Schema version",
                Path = "$"
            });
        }
        else
        {
            var schemaUri = jsonObject["$schema"]?.ToString();
            if (!IsValidSchemaUri(schemaUri))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Unknown or potentially invalid schema URI: {schemaUri}",
                    Path = "$.$schema"
                });
            }
        }
        if (!jsonObject.ContainsKey("type") && !jsonObject.ContainsKey("$ref") && 
            !jsonObject.ContainsKey("oneOf") && !jsonObject.ContainsKey("anyOf") && 
            !jsonObject.ContainsKey("allOf"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Schema should have 'type', '$ref', 'oneOf', 'anyOf', or 'allOf' property",
                Path = "$"
            });
        }
    }

    private void ValidateSchemaProperties(JsonSchema jsonSchema, JObject jsonObject, ValidationResult result)
    {
        if (!jsonObject.ContainsKey("title"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Consider adding 'title' property for better documentation",
                Path = "$"
            });
        }

        if (!jsonObject.ContainsKey("description"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Consider adding 'description' property for better documentation",
                Path = "$"
            });
        }
        var type = jsonObject["type"]?.ToString();
        if (type == "object")
        {
            if (!jsonObject.ContainsKey("properties") && !jsonObject.ContainsKey("additionalProperties"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "Object type schema should have 'properties' or 'additionalProperties'",
                    Path = "$"
                });
            }
            if (jsonObject["properties"] is JObject properties)
            {
                foreach (var prop in properties.Properties())
                {
                    ValidatePropertyDefinition(prop, $"$.properties.{prop.Name}", result);
                }
            }
        }
        if (type == "array")
        {
            if (!jsonObject.ContainsKey("items"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "Array type schema should have 'items' property",
                    Path = "$"
                });
            }
        }
    }

    private void ValidatePropertyDefinition(JProperty property, string path, ValidationResult result)
    {
        if (property.Value is not JObject propDef)
        {
            result.Errors.Add(new ValidationError
            {
                Message = $"Property definition must be an object",
                Path = path,
                ErrorType = ValidationErrorType.Schema
            });
            return;
        }
        if (!propDef.ContainsKey("type") && !propDef.ContainsKey("$ref") &&
            !propDef.ContainsKey("oneOf") && !propDef.ContainsKey("anyOf") && 
            !propDef.ContainsKey("allOf") && !propDef.ContainsKey("enum"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = $"Property '{property.Name}' should have 'type' or '$ref'",
                Path = path
            });
        }
        if (propDef["type"]?.ToString() == "object" && propDef["properties"] is JObject nestedProps)
        {
            foreach (var nestedProp in nestedProps.Properties())
            {
                ValidatePropertyDefinition(nestedProp, $"{path}.properties.{nestedProp.Name}", result);
            }
        }
    }

    private void ValidateSchemaKeywords(JObject jsonObject, ValidationResult result)
    {
        var validKeywords = new HashSet<string>
        {
            "$schema", "$id", "$ref", "$defs", "definitions",
            "title", "description", "type", "properties", "required",
            "additionalProperties", "items", "additionalItems",
            "minItems", "maxItems", "uniqueItems", "contains",
            "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum",
            "minLength", "maxLength", "pattern", "format",
            "enum", "const", "default", "examples",
            "allOf", "anyOf", "oneOf", "not", "if", "then", "else",
            "propertyNames", "minProperties", "maxProperties",
            "dependencies", "dependentRequired", "dependentSchemas",
            "$comment", "contentMediaType", "contentEncoding",
            "deprecated", "readOnly", "writeOnly"
        };

        foreach (var property in jsonObject.Properties())
        {
            if (!validKeywords.Contains(property.Name) && !property.Name.StartsWith("x-"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = $"Unknown schema keyword: '{property.Name}'",
                    Path = $"$.{property.Name}"
                });
            }
        }
    }

    private bool IsValidSchemaUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return false;

        var validSchemas = new[]
        {
            "http://json-schema.org/draft-04/schema",
            "http://json-schema.org/draft-06/schema",
            "http://json-schema.org/draft-07/schema",
            "https://json-schema.org/draft/2019-09/schema",
            "https://json-schema.org/draft/2020-12/schema"
        };

        return validSchemas.Any(s => uri.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}