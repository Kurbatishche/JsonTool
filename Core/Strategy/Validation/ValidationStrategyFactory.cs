namespace JsonTool.Core.Strategy.Validation;
public static class ValidationStrategyFactory
{
    public static IValidationStrategy Create(ValidationType type)
    {
        return type switch
        {
            ValidationType.Syntax => new SyntaxValidationStrategy(),
            ValidationType.Schema => new SchemaValidationStrategy(),
            ValidationType.JsonBySchema => new JsonBySchemaValidationStrategy(),
            _ => throw new ArgumentException($"Unknown validation type: {type}")
        };
    }
    public static ValidationContext CreateContext(ValidationType type)
    {
        return new ValidationContext(Create(type));
    }
    public static ValidationContext CreateChainContext()
    {
        return new ValidationContext(new SyntaxValidationStrategy());
    }
    public static IEnumerable<IValidationStrategy> GetAllStrategies()
    {
        yield return new SyntaxValidationStrategy();
        yield return new SchemaValidationStrategy();
        yield return new JsonBySchemaValidationStrategy();
    }
}
public enum ValidationType
{
    Syntax,
    Schema,
    JsonBySchema
}