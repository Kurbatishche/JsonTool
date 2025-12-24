using JsonTool.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace JsonTool.Core.TemplateMethod;
public class JsonSchemaValidator : JsonValidatorBase
{
    protected override bool ValidateSyntax(string jsonContent, ValidationResult result)
    {
        try
        {
            JToken.Parse(jsonContent);
            return true;
        }
        catch (JsonReaderException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new Models.ValidationError
            {
                Message = ex.Message,
                Path = ex.Path ?? "$",
                LineNumber = ex.LineNumber,
                Column = ex.LinePosition
            });
            return false;
        }
    }

    protected override void ValidateSpecific(string jsonContent, ValidationResult result)
    {
        try
        {
            var schema = JSchema.Parse(jsonContent);
            var jsonObject = JObject.Parse(jsonContent);
            
            if (!jsonObject.ContainsKey("type") && !jsonObject.ContainsKey("$ref"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "Schema should have a 'type' or '$ref' property",
                    Path = "$"
                });
            }

            if (!jsonObject.ContainsKey("$schema"))
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Message = "Schema should specify '$schema' to indicate JSON Schema version",
                    Path = "$"
                });
            }
        }
        catch (JSchemaReaderException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new Models.ValidationError
            {
                Message = $"Invalid JSON Schema: {ex.Message}",
                Path = ex.Path ?? "$",
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition
            });
        }
    }

    protected override void PerformAdditionalValidation(string jsonContent, ValidationResult result)
    {
        var jsonObject = JObject.Parse(jsonContent);
        if (!jsonObject.ContainsKey("title"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Consider adding a 'title' property for better documentation",
                Path = "$"
            });
        }

        if (!jsonObject.ContainsKey("description"))
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = "Consider adding a 'description' property for better documentation",
                Path = "$"
            });
        }
    }
}