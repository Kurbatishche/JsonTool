namespace JsonTool.Core.Models;
public class ValidationError
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public ValidationErrorType ErrorType { get; set; } = ValidationErrorType.Error;
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public int? LinePosition { get; set; }
    public int? Column { get => LinePosition; set => LinePosition = value; }
    public string? SchemaPath { get; set; }
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string? SchemaName { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Icon => ErrorType switch
    {
        ValidationErrorType.Error => "❌",
        ValidationErrorType.Warning => "⚠",
        ValidationErrorType.Info => "ℹ",
        ValidationErrorType.Success => "✓",
        _ => "?"
    };
    public string Color => ErrorType switch
    {
        ValidationErrorType.Error => "#F48771",
        ValidationErrorType.Warning => "#CCA700",
        ValidationErrorType.Info => "#75BEFF",
        ValidationErrorType.Success => "#89D185",
        _ => "#CCCCCC"
    };
    public string DisplayMessage
    {
        get
        {
            var location = "";
            if (LineNumber.HasValue)
            {
                location = LinePosition.HasValue
                    ? $" (Line {LineNumber}, Col {LinePosition})"
                    : $" (Line {LineNumber})";
            }
            else if (!string.IsNullOrEmpty(Path))
            {
                location = $" at {Path}";
            }

            return $"{Message}{location}";
        }
    }
    public string FullMessage
    {
        get
        {
            var parts = new List<string> { Message };

            if (!string.IsNullOrEmpty(Path))
                parts.Add($"Path: {Path}");

            if (LineNumber.HasValue)
                parts.Add($"Line: {LineNumber}");

            if (!string.IsNullOrEmpty(ExpectedValue))
                parts.Add($"Expected: {ExpectedValue}");

            if (!string.IsNullOrEmpty(ActualValue))
                parts.Add($"Actual: {ActualValue}");

            return string.Join(" | ", parts);
        }
    }

    public override string ToString() => DisplayMessage;
    public static ValidationError SyntaxError(string message, int? line = null, int? position = null)
    {
        return new ValidationError
        {
            ErrorType = ValidationErrorType.Error,
            ErrorCode = "SYNTAX_ERROR",
            Message = message,
            LineNumber = line,
            LinePosition = position
        };
    }
    public static ValidationError SchemaError(string message, string path)
    {
        return new ValidationError
        {
            ErrorType = ValidationErrorType.Error,
            ErrorCode = "SCHEMA_ERROR",
            Message = message,
            Path = path
        };
    }
    public static ValidationError Warning(string message, string? path = null)
    {
        return new ValidationError
        {
            ErrorType = ValidationErrorType.Warning,
            ErrorCode = "WARNING",
            Message = message,
            Path = path ?? string.Empty
        };
    }
    public static ValidationError Info(string message)
    {
        return new ValidationError
        {
            ErrorType = ValidationErrorType.Info,
            ErrorCode = "INFO",
            Message = message
        };
    }
    public static ValidationError Success(string message)
    {
        return new ValidationError
        {
            ErrorType = ValidationErrorType.Success,
            ErrorCode = "SUCCESS",
            Message = message
        };
    }
}
public enum ValidationErrorType
{
    Error,
    Warning,
    Info,
    Success,
    Syntax,
    Schema,
    DataValidation,
    Configuration,
    Unknown
}