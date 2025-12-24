using JsonTool.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace JsonTool.Core.TemplateMethod;
public class JsonDataValidator : JsonValidatorBase
{
    private readonly string _schemaContent;

    public JsonDataValidator(string schemaContent)
    {
        _schemaContent = schemaContent;
    }

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
            var schema = JSchema.Parse(_schemaContent);
            var jsonData = JToken.Parse(jsonContent);

            IList<string> errorMessages;
            bool isValid = jsonData.IsValid(schema, out errorMessages);

            if (!isValid)
            {
                result.IsValid = false;
                foreach (var error in errorMessages)
                {
                    result.Errors.Add(new Models.ValidationError
                    {
                        Message = error,
                        Path = "$"
                    });
                }
            }
        }
        catch (JSchemaReaderException ex)
        {
            result.IsValid = false;
            result.Errors.Add(new Models.ValidationError
            {
                Message = $"Invalid schema: {ex.Message}",
                Path = "$"
            });
        }
    }
}