using JsonTool.Core.Models;

namespace JsonTool.Core.TemplateMethod;
public abstract class JsonValidatorBase
{
    public ValidationResult Validate(string jsonContent)
    {
        var result = new ValidationResult { IsValid = true };
        if (!ValidateNotEmpty(jsonContent, result))
        {
            return result;
        }
        if (!ValidateSyntax(jsonContent, result))
        {
            return result;
        }
        ValidateSpecific(jsonContent, result);
        PerformAdditionalValidation(jsonContent, result);

        return result;
    }

    private bool ValidateNotEmpty(string jsonContent, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                Message = "JSON content is empty",
                Path = "$",
                LineNumber = 1,
                Column = 1
            });
            return false;
        }
        return true;
    }
    protected abstract bool ValidateSyntax(string jsonContent, ValidationResult result);
    protected abstract void ValidateSpecific(string jsonContent, ValidationResult result);
    protected virtual void PerformAdditionalValidation(string jsonContent, ValidationResult result)
    {
    }
}