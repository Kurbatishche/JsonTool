using Newtonsoft.Json.Linq;

namespace JsonTool.Core.Flyweight;
public class SchemaFlyweightParser
{
    private readonly SchemaPropertyFlyweightFactory _factory;
    public SchemaPropertyFlyweightFactory Factory => _factory;

    public SchemaFlyweightParser(SchemaPropertyFlyweightFactory? factory = null)
    {
        _factory = factory ?? new SchemaPropertyFlyweightFactory();
    }
    public List<SchemaPropertyContext> Parse(string jsonSchema)
    {
        var schema = JObject.Parse(jsonSchema);
        return ParseProperties(schema, "$");
    }
    public List<SchemaPropertyContext> Parse(JObject schema)
    {
        return ParseProperties(schema, "$");
    }

    private List<SchemaPropertyContext> ParseProperties(JObject schema, string parentPath)
    {
        var result = new List<SchemaPropertyContext>();
        var properties = schema["properties"] as JObject;
        
        if (properties == null) return result;

        var required = (schema["required"] as JArray)?
            .Select(r => r.ToString())
            .ToHashSet() ?? new HashSet<string>();

        foreach (var prop in properties.Properties())
        {
            var context = ParseProperty(prop, parentPath, required);
            result.Add(context);
        }

        return result;
    }

    private SchemaPropertyContext ParseProperty(JProperty prop, string parentPath, HashSet<string> required)
    {
        var propDef = prop.Value as JObject ?? new JObject();
        var path = $"{parentPath}.{prop.Name}";
        var type = propDef["type"]?.ToString() ?? "any";
        var format = propDef["format"]?.ToString();
        var pattern = propDef["pattern"]?.ToString();
        var flyweight = _factory.GetOrCreate(type, format, pattern);
        var context = new SchemaPropertyContext(flyweight, prop.Name, path)
        {
            Description = propDef["description"]?.ToString(),
            Example = propDef["example"]?.ToString() ?? propDef["default"]?.ToString(),
            DefaultValue = propDef["default"]?.ToObject<object>(),
            IsRequired = required.Contains(prop.Name),
            Minimum = propDef["minimum"]?.ToObject<decimal?>(),
            Maximum = propDef["maximum"]?.ToObject<decimal?>(),
            MinLength = propDef["minLength"]?.ToObject<int?>(),
            MaxLength = propDef["maxLength"]?.ToObject<int?>(),
            EnumValues = (propDef["enum"] as JArray)?.Select(e => e.ToString()).ToList()
        };
        if (propDef["properties"] is JObject nestedProps)
        {
            var nestedRequired = (propDef["required"] as JArray)?
                .Select(r => r.ToString())
                .ToHashSet() ?? new HashSet<string>();

            foreach (var nestedProp in nestedProps.Properties())
            {
                var childContext = ParseProperty(nestedProp, path, nestedRequired);
                context.Children.Add(childContext);
            }
        }
        if (type == "array" && propDef["items"] is JObject items)
        {
            var itemType = items["type"]?.ToString() ?? "any";
            var itemFormat = items["format"]?.ToString();
            var itemPattern = items["pattern"]?.ToString();
            _factory.GetOrCreate(itemType, itemFormat, itemPattern);

            if (items["properties"] is JObject itemProps)
            {
                var itemRequired = (items["required"] as JArray)?
                    .Select(r => r.ToString())
                    .ToHashSet() ?? new HashSet<string>();

                foreach (var itemProp in itemProps.Properties())
                {
                    var childContext = ParseProperty(itemProp, $"{path}[]", itemRequired);
                    context.Children.Add(childContext);
                }
            }
        }

        return context;
    }
    public MemoryComparisonResult CompareMemoryUsage(string jsonSchema)
    {
        var properties = Parse(jsonSchema);
        var flyweightSize = CalculateFlyweightSize(properties);
        var withoutFlyweightSize = CalculateWithoutFlyweightSize(properties);

        var stats = _factory.GetStatistics();

        return new MemoryComparisonResult
        {
            PropertyCount = CountAllProperties(properties),
            UniqueFlyweights = _factory.CacheCount,
            WithFlyweightBytes = flyweightSize,
            WithoutFlyweightBytes = withoutFlyweightSize,
            SavedBytes = withoutFlyweightSize - flyweightSize,
            SavingsPercent = withoutFlyweightSize > 0 
                ? (double)(withoutFlyweightSize - flyweightSize) / withoutFlyweightSize * 100 
                : 0,
            CacheHitRate = stats.HitRate
        };
    }

    private int CalculateFlyweightSize(List<SchemaPropertyContext> properties)
    {
        var size = 0;
        foreach (var flyweight in _factory.GetAll())
        {
            size += flyweight.GetApproximateSize();
        }
        size += properties.Sum(p => CalculateContextSize(p));

        return size;
    }

    private int CalculateContextSize(SchemaPropertyContext context)
    {
        var size = 24; // Object header
        size += 8; // Reference to flyweight
        size += (context.Name?.Length ?? 0) * 2;
        size += (context.Path?.Length ?? 0) * 2;
        size += (context.Description?.Length ?? 0) * 2;
        size += (context.Example?.Length ?? 0) * 2;
        size += 32; // Various nullable fields

        if (context.EnumValues != null)
        {
            size += context.EnumValues.Sum(e => (e?.Length ?? 0) * 2 + 8);
        }

        foreach (var child in context.Children)
        {
            size += CalculateContextSize(child);
        }

        return size;
    }

    private int CalculateWithoutFlyweightSize(List<SchemaPropertyContext> properties)
    {
        return properties.Sum(p => CalculateFullPropertySize(p));
    }

    private int CalculateFullPropertySize(SchemaPropertyContext context)
    {
        var size = 24; // Object header
        size += (context.Type?.Length ?? 0) * 2;
        size += (context.Format?.Length ?? 0) * 2;
        size += (context.Pattern?.Length ?? 0) * 2;
        size += (context.Name?.Length ?? 0) * 2;
        size += (context.Path?.Length ?? 0) * 2;
        size += (context.Description?.Length ?? 0) * 2;
        size += (context.Example?.Length ?? 0) * 2;
        size += 32; // Various nullable fields

        if (context.EnumValues != null)
        {
            size += context.EnumValues.Sum(e => (e?.Length ?? 0) * 2 + 8);
        }

        foreach (var child in context.Children)
        {
            size += CalculateFullPropertySize(child);
        }

        return size;
    }

    private int CountAllProperties(List<SchemaPropertyContext> properties)
    {
        return properties.Count + properties.Sum(p => CountAllProperties(p.Children));
    }
}
public class MemoryComparisonResult
{
    public int PropertyCount { get; set; }
    public int UniqueFlyweights { get; set; }
    public int WithFlyweightBytes { get; set; }
    public int WithoutFlyweightBytes { get; set; }
    public int SavedBytes { get; set; }
    public double SavingsPercent { get; set; }
    public double CacheHitRate { get; set; }

    public override string ToString()
    {
        return $"""
            Memory Comparison:
              Properties: {PropertyCount}
              Unique Flyweights: {UniqueFlyweights}
              With Flyweight: {FormatBytes(WithFlyweightBytes)}
              Without Flyweight: {FormatBytes(WithoutFlyweightBytes)}
              Saved: {FormatBytes(SavedBytes)} ({SavingsPercent:F1}%)
              Cache Hit Rate: {CacheHitRate:F1}%
            """;
    }

    private static string FormatBytes(int bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}