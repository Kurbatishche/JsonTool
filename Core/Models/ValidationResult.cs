namespace JsonTool.Core.Models;
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
    public string StrategyUsed { get; set; } = string.Empty;
    public int ErrorCount => Errors.Count;
    public int WarningCount => Warnings.Count;
    public bool HasCriticalErrors => Errors.Any(e => 
        e.ErrorType == ValidationErrorType.Error);
    public string Summary => IsValid 
        ? $"Valid ({WarningCount} warning(s))" 
        : $"Invalid ({ErrorCount} error(s), {WarningCount} warning(s))";
}
public class ValidationWarning
{
    public string Message { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"[Warning] {Path}: {Message}";
    }
}