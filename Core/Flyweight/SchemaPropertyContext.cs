namespace JsonTool.Core.Flyweight;
public class SchemaPropertyContext
{
    public SchemaPropertyFlyweight Flyweight { get; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string? Description { get; set; }
    public string? Example { get; set; }
    public object? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public List<string>? EnumValues { get; set; }
    public List<SchemaPropertyContext> Children { get; } = new();
    public Dictionary<string, object> Metadata { get; } = new();

    public SchemaPropertyContext(SchemaPropertyFlyweight flyweight, string name, string path)
    {
        Flyweight = flyweight ?? throw new ArgumentNullException(nameof(flyweight));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }
    public string Type => Flyweight.Type;
    public string? Format => Flyweight.Format;
    public string? Pattern => Flyweight.Pattern;
    public int GetApproximateSize()
    {
        var size = 24; // Object header
        size += 8; // Reference to flyweight (не враховуємо розмір flyweight - він спільний)
        size += (Name?.Length ?? 0) * 2;
        size += (Path?.Length ?? 0) * 2;
        size += (Description?.Length ?? 0) * 2;
        size += (Example?.Length ?? 0) * 2;
        size += 16; // booleans, nullables
        
        if (EnumValues != null)
        {
            size += EnumValues.Sum(e => (e?.Length ?? 0) * 2 + 8);
        }

        foreach (var child in Children)
        {
            size += child.GetApproximateSize();
        }

        return size;
    }

    public override string ToString()
    {
        var required = IsRequired ? " *" : "";
        return $"{Name}: {Flyweight.GetDisplayType()}{required}";
    }
}