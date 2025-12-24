using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy.Validation;
public interface IValidationStrategy
{
    string StrategyName { get; }
    string Description { get; }
    ValidationResult Validate(string content, string? schema = null);
    Task<ValidationResult> ValidateAsync(string content, string? schema = null);
    bool CanValidate(string content, string? schema = null);
}