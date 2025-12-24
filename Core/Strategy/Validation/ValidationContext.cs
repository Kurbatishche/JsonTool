using JsonTool.Core.Models;

namespace JsonTool.Core.Strategy.Validation;
public class ValidationContext
{
    private IValidationStrategy _strategy;
    private readonly List<ValidationResult> _validationHistory;
    private readonly int _maxHistorySize;

    public ValidationContext(IValidationStrategy strategy, int maxHistorySize = 10)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _validationHistory = new List<ValidationResult>();
        _maxHistorySize = maxHistorySize;
    }
    public IValidationStrategy CurrentStrategy => _strategy;
    public string StrategyName => _strategy.StrategyName;
    public string StrategyDescription => _strategy.Description;
    public IReadOnlyList<ValidationResult> ValidationHistory => _validationHistory.AsReadOnly();
    public ValidationResult? LastResult => _validationHistory.LastOrDefault();
    public void SetStrategy(IValidationStrategy strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }
    public ValidationResult Validate(string content, string? schema = null)
    {
        if (!_strategy.CanValidate(content, schema))
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Message = $"Strategy '{_strategy.StrategyName}' cannot validate with provided parameters",
                        Path = "$",
                        ErrorType = ValidationErrorType.Configuration
                    }
                }
            };
        }

        var result = _strategy.Validate(content, schema);
        result.ValidatedAt = DateTime.Now;
        result.StrategyUsed = _strategy.StrategyName;
        
        AddToHistory(result);
        return result;
    }
    public async Task<ValidationResult> ValidateAsync(string content, string? schema = null)
    {
        if (!_strategy.CanValidate(content, schema))
        {
            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Message = $"Strategy '{_strategy.StrategyName}' cannot validate with provided parameters",
                        Path = "$",
                        ErrorType = ValidationErrorType.Configuration
                    }
                }
            };
        }

        var result = await _strategy.ValidateAsync(content, schema);
        result.ValidatedAt = DateTime.Now;
        result.StrategyUsed = _strategy.StrategyName;
        
        AddToHistory(result);
        return result;
    }
    public async Task<CompositeValidationResult> ValidateWithAllStrategiesAsync(
        string content, 
        string? schema = null,
        IEnumerable<IValidationStrategy>? strategies = null)
    {
        var allStrategies = strategies ?? GetDefaultStrategies();
        var compositeResult = new CompositeValidationResult();

        foreach (var strategy in allStrategies)
        {
            if (strategy.CanValidate(content, schema))
            {
                var result = await strategy.ValidateAsync(content, schema);
                result.ValidatedAt = DateTime.Now;
                result.StrategyUsed = strategy.StrategyName;
                compositeResult.Results[strategy.StrategyName] = result;
            }
        }

        compositeResult.CalculateOverallStatus();
        return compositeResult;
    }
    public async Task<ValidationResult> ValidateChainAsync(
        string content,
        string? schema = null,
        IEnumerable<IValidationStrategy>? strategies = null)
    {
        var allStrategies = strategies ?? GetDefaultStrategies();

        foreach (var strategy in allStrategies)
        {
            if (strategy.CanValidate(content, schema))
            {
                var result = await strategy.ValidateAsync(content, schema);
                result.ValidatedAt = DateTime.Now;
                result.StrategyUsed = strategy.StrategyName;

                if (!result.IsValid)
                {
                    AddToHistory(result);
                    return result;
                }
            }
        }

        var successResult = new ValidationResult
        {
            IsValid = true,
            ValidatedAt = DateTime.Now,
            StrategyUsed = "Chain Validation"
        };
        
        AddToHistory(successResult);
        return successResult;
    }
    public void ClearHistory()
    {
        _validationHistory.Clear();
    }

    private void AddToHistory(ValidationResult result)
    {
        _validationHistory.Add(result);
        while (_validationHistory.Count > _maxHistorySize)
        {
            _validationHistory.RemoveAt(0);
        }
    }

    private static IEnumerable<IValidationStrategy> GetDefaultStrategies()
    {
        return new IValidationStrategy[]
        {
            new SyntaxValidationStrategy(),
            new SchemaValidationStrategy(),
            new JsonBySchemaValidationStrategy()
        };
    }
}
public class CompositeValidationResult
{
    public Dictionary<string, ValidationResult> Results { get; } = new();
    public bool IsValid { get; private set; }
    public int TotalErrors { get; private set; }
    public int TotalWarnings { get; private set; }

    public void CalculateOverallStatus()
    {
        IsValid = Results.Values.All(r => r.IsValid);
        TotalErrors = Results.Values.Sum(r => r.Errors.Count);
        TotalWarnings = Results.Values.Sum(r => r.Warnings.Count);
    }

    public IEnumerable<ValidationError> GetAllErrors()
    {
        return Results.Values.SelectMany(r => r.Errors);
    }

    public IEnumerable<ValidationWarning> GetAllWarnings()
    {
        return Results.Values.SelectMany(r => r.Warnings);
    }
}