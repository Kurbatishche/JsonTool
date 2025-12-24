using JsonTool.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;

namespace JsonTool.Core.Strategy.Validation;
public class JsonBySchemaValidationStrategy : IValidationStrategy
{
    private readonly string? _schema;

    public JsonBySchemaValidationStrategy() { }
    
    public JsonBySchemaValidationStrategy(string schema)
    {
        _schema = schema;
    }

    public string StrategyName => "JSON by Schema Validation";
    public string Description => "Validates JSON data against a JSON Schema";

    public bool CanValidate(string content, string? schema = null)
    {
        return !string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(schema);
    }

    public ValidationResult Validate(string content, string? schema = null)
    {
        return ValidateAsync(content, schema).GetAwaiter().GetResult();
    }

    public async Task<ValidationResult> ValidateAsync(string content, string? schema = null)
    {
        var result = new ValidationResult { IsValid = true };
        if (string.IsNullOrEmpty(schema))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = "Schema is required for JSON validation",
                Path = "$",
                ErrorType = ValidationErrorType.Schema
            });
            return result;
        }
        var syntaxStrategy = new SyntaxValidationStrategy();
        var syntaxResult = syntaxStrategy.Validate(content);
        
        if (!syntaxResult.IsValid)
        {
            foreach (var error in syntaxResult.Errors)
            {
                error.Message = $"[Data] {error.Message}";
            }
            return syntaxResult;
        }
        var schemaSyntaxResult = syntaxStrategy.Validate(schema);
        if (!schemaSyntaxResult.IsValid)
        {
            foreach (var error in schemaSyntaxResult.Errors)
            {
                error.Message = $"[Schema] {error.Message}";
            }
            return schemaSyntaxResult;
        }

        try
        {
            var jsonSchema = await JsonSchema.FromJsonAsync(schema);
            var errors = jsonSchema.Validate(content);

            if (errors.Count > 0)
            {
                result.IsValid = false;
                foreach (var error in errors)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Message = FormatValidationError(error),
                        Path = error.Path ?? "$",
                        LineNumber = error.LineNumber,
                        Column = error.LinePosition,
                        ErrorType = ValidationErrorType.DataValidation,
                        SchemaPath = error.Schema?.DocumentPath
                    });
                }
            }
            await PerformAdditionalValidations(content, jsonSchema, result);
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
                Message = $"Schema error: {ex.Message}",
                Path = "$",
                ErrorType = ValidationErrorType.Schema
            });
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = $"Validation error: {ex.Message}",
                Path = "$",
                ErrorType = ValidationErrorType.DataValidation
            });
        }

        return result;
    }

    private string FormatValidationError(NJsonSchema.Validation.ValidationError error)
    {
        var message = error.Kind.ToString();
        message = error.Kind switch
        {
            NJsonSchema.Validation.ValidationErrorKind.StringTooShort => 
                $"String is too short. Minimum length: {error.Schema?.MinLength}",
            NJsonSchema.Validation.ValidationErrorKind.StringTooLong => 
                $"String is too long. Maximum length: {error.Schema?.MaxLength}",
            NJsonSchema.Validation.ValidationErrorKind.NumberTooSmall => 
                $"Number is too small. Minimum: {error.Schema?.Minimum}",
            NJsonSchema.Validation.ValidationErrorKind.NumberTooBig => 
                $"Number is too big. Maximum: {error.Schema?.Maximum}",
            NJsonSchema.Validation.ValidationErrorKind.PatternMismatch => 
                $"String does not match pattern: {error.Schema?.Pattern}",
            NJsonSchema.Validation.ValidationErrorKind.StringExpected => 
                "Expected a string value",
            NJsonSchema.Validation.ValidationErrorKind.NumberExpected => 
                "Expected a number value",
            NJsonSchema.Validation.ValidationErrorKind.BooleanExpected => 
                "Expected a boolean value",
            NJsonSchema.Validation.ValidationErrorKind.IntegerExpected => 
                "Expected an integer value",
            NJsonSchema.Validation.ValidationErrorKind.ArrayExpected => 
                "Expected an array",
            NJsonSchema.Validation.ValidationErrorKind.ObjectExpected => 
                "Expected an object",
            NJsonSchema.Validation.ValidationErrorKind.NullExpected => 
                "Expected null",
            NJsonSchema.Validation.ValidationErrorKind.PropertyRequired => 
                $"Required property is missing",
            NJsonSchema.Validation.ValidationErrorKind.AdditionalPropertiesNotValid =>
                "Additional properties are not allowed",
            NJsonSchema.Validation.ValidationErrorKind.NotInEnumeration =>
                $"Value is not in the allowed enumeration",
            NJsonSchema.Validation.ValidationErrorKind.ArrayItemNotValid =>
                "Array item does not match schema",
            NJsonSchema.Validation.ValidationErrorKind.TooManyItems =>
                $"Array has too many items. Maximum: {error.Schema?.MaxItems}",
            NJsonSchema.Validation.ValidationErrorKind.TooFewItems =>
                $"Array has too few items. Minimum: {error.Schema?.MinItems}",
            NJsonSchema.Validation.ValidationErrorKind.DateTimeExpected =>
                "Expected a valid date-time string",
            NJsonSchema.Validation.ValidationErrorKind.DateExpected =>
                "Expected a valid date string",
            NJsonSchema.Validation.ValidationErrorKind.TimeExpected =>
                "Expected a valid time string",
            NJsonSchema.Validation.ValidationErrorKind.EmailExpected =>
                "Expected a valid email address",
            NJsonSchema.Validation.ValidationErrorKind.UriExpected =>
                "Expected a valid URI",
            NJsonSchema.Validation.ValidationErrorKind.IpV4Expected =>
                "Expected a valid IPv4 address",
            NJsonSchema.Validation.ValidationErrorKind.IpV6Expected =>
                "Expected a valid IPv6 address",
            NJsonSchema.Validation.ValidationErrorKind.GuidExpected =>
                "Expected a valid GUID/UUID",
            _ => error.ToString() ?? message
        };
        if (!string.IsNullOrEmpty(error.Property))
        {
            message = $"'{error.Property}': {message}";
        }

        return message;
    }

    private async Task PerformAdditionalValidations(string content, JsonSchema schema, ValidationResult result)
    {
        var jsonData = JToken.Parse(content);
        await ValidateFormatsRecursively(jsonData, schema, "$", result);
    }

    private Task ValidateFormatsRecursively(JToken data, JsonSchema schema, string path, ValidationResult result)
    {
        if (data is JObject obj && schema.Properties != null)
        {
            foreach (var property in obj.Properties())
            {
                if (schema.Properties.TryGetValue(property.Name, out var propSchema))
                {
                    ValidateFormat(property.Value, propSchema, $"{path}.{property.Name}", result);
                }
            }
        }
        else if (data is JArray array && schema.Item != null)
        {
        }

        return Task.CompletedTask;
    }

    private void ValidateFormat(JToken value, JsonSchemaProperty propSchema, string path, ValidationResult result)
    {
        if (value.Type != JTokenType.String || string.IsNullOrEmpty(propSchema.Format))
            return;

        var stringValue = value.ToString();
        var isValid = propSchema.Format switch
        {
            "date" => DateTime.TryParseExact(stringValue, "yyyy-MM-dd", null, 
                System.Globalization.DateTimeStyles.None, out _),
            "date-time" => DateTime.TryParse(stringValue, out _),
            "time" => TimeSpan.TryParse(stringValue, out _),
            "email" => IsValidEmail(stringValue),
            "uri" => Uri.TryCreate(stringValue, UriKind.Absolute, out _),
            "uuid" => Guid.TryParse(stringValue, out _),
            "ipv4" => System.Net.IPAddress.TryParse(stringValue, out var ip) && 
                      ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork,
            "ipv6" => System.Net.IPAddress.TryParse(stringValue, out var ip6) && 
                      ip6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6,
            _ => true
        };

        if (!isValid)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Message = $"Value does not match format '{propSchema.Format}': {stringValue}",
                Path = path
            });
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}