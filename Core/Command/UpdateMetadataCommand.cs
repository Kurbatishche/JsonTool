using Newtonsoft.Json.Linq;

namespace JsonTool.Core.Command;
public class UpdateMetadataCommand : SchemaCommandBase
{
    private readonly JObject _schema;
    private readonly string _propertyPath;
    private readonly PropertyMetadata _newMetadata;
    private PropertyMetadata? _previousMetadata;

    public override string Description => $"Update metadata for '{_propertyPath}'";
    public string PropertyPath => _propertyPath;
    public PropertyMetadata NewMetadata => _newMetadata;
    public PropertyMetadata? PreviousMetadata => _previousMetadata;
    public UpdateMetadataCommand(JObject schema, string propertyPath, PropertyMetadata newMetadata)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
        _newMetadata = newMetadata ?? throw new ArgumentNullException(nameof(newMetadata));
    }
    public static UpdateMetadataCommand CreateDescriptionUpdate(JObject schema, string propertyPath, string description)
    {
        return new UpdateMetadataCommand(schema, propertyPath, new PropertyMetadata { Description = description });
    }
    public static UpdateMetadataCommand CreateTypeUpdate(JObject schema, string propertyPath, string type)
    {
        return new UpdateMetadataCommand(schema, propertyPath, new PropertyMetadata { Type = type });
    }
    public static UpdateMetadataCommand CreateExampleUpdate(JObject schema, string propertyPath, object? example)
    {
        return new UpdateMetadataCommand(schema, propertyPath, new PropertyMetadata { Example = example });
    }

    public override bool CanExecute()
    {
        if (!base.CanExecute()) return false;
        var token = _schema.SelectToken(_propertyPath);
        return token is JObject;
    }

    protected override void ExecuteCore()
    {
        var propertyToken = _schema.SelectToken(_propertyPath) as JObject;
        if (propertyToken == null)
        {
            throw new InvalidOperationException($"Property at path '{_propertyPath}' not found or is not an object");
        }
        _previousMetadata = ExtractMetadata(propertyToken);
        ApplyMetadata(propertyToken, _newMetadata);
    }

    protected override void UndoCore()
    {
        if (_previousMetadata == null) return;

        var propertyToken = _schema.SelectToken(_propertyPath) as JObject;
        if (propertyToken == null) return;
        ApplyMetadata(propertyToken, _previousMetadata, restoreNulls: true);
    }

    private static PropertyMetadata ExtractMetadata(JObject propertyToken)
    {
        return new PropertyMetadata
        {
            Type = propertyToken["type"]?.ToString(),
            Description = propertyToken["description"]?.ToString(),
            Format = propertyToken["format"]?.ToString(),
            Example = propertyToken["example"]?.ToObject<object>(),
            Default = propertyToken["default"]?.ToObject<object>(),
            Minimum = propertyToken["minimum"]?.ToObject<decimal?>(),
            Maximum = propertyToken["maximum"]?.ToObject<decimal?>(),
            MinLength = propertyToken["minLength"]?.ToObject<int?>(),
            MaxLength = propertyToken["maxLength"]?.ToObject<int?>(),
            Pattern = propertyToken["pattern"]?.ToString(),
            Enum = propertyToken["enum"]?.ToObject<List<object>>(),
            Title = propertyToken["title"]?.ToString(),
            Deprecated = propertyToken["deprecated"]?.ToObject<bool?>(),
            ReadOnly = propertyToken["readOnly"]?.ToObject<bool?>(),
            WriteOnly = propertyToken["writeOnly"]?.ToObject<bool?>()
        };
    }

    private static void ApplyMetadata(JObject propertyToken, PropertyMetadata metadata, bool restoreNulls = false)
    {
        if (metadata.Type != null)
        {
            propertyToken["type"] = metadata.Type;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("type");
        }
        if (metadata.Description != null)
        {
            propertyToken["description"] = metadata.Description;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("description");
        }
        if (metadata.Format != null)
        {
            propertyToken["format"] = metadata.Format;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("format");
        }
        if (metadata.Example != null)
        {
            propertyToken["example"] = JToken.FromObject(metadata.Example);
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("example");
        }
        if (metadata.Default != null)
        {
            propertyToken["default"] = JToken.FromObject(metadata.Default);
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("default");
        }
        if (metadata.Minimum.HasValue)
        {
            propertyToken["minimum"] = metadata.Minimum.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("minimum");
        }
        if (metadata.Maximum.HasValue)
        {
            propertyToken["maximum"] = metadata.Maximum.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("maximum");
        }
        if (metadata.MinLength.HasValue)
        {
            propertyToken["minLength"] = metadata.MinLength.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("minLength");
        }
        if (metadata.MaxLength.HasValue)
        {
            propertyToken["maxLength"] = metadata.MaxLength.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("maxLength");
        }
        if (metadata.Pattern != null)
        {
            propertyToken["pattern"] = metadata.Pattern;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("pattern");
        }
        if (metadata.Enum != null)
        {
            propertyToken["enum"] = JArray.FromObject(metadata.Enum);
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("enum");
        }
        if (metadata.Title != null)
        {
            propertyToken["title"] = metadata.Title;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("title");
        }
        if (metadata.Deprecated.HasValue)
        {
            propertyToken["deprecated"] = metadata.Deprecated.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("deprecated");
        }
        if (metadata.ReadOnly.HasValue)
        {
            propertyToken["readOnly"] = metadata.ReadOnly.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("readOnly");
        }
        if (metadata.WriteOnly.HasValue)
        {
            propertyToken["writeOnly"] = metadata.WriteOnly.Value;
        }
        else if (restoreNulls)
        {
            propertyToken.Remove("writeOnly");
        }
    }
}
public class PropertyMetadata
{
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Format { get; set; }
    public object? Example { get; set; }
    public object? Default { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }
    public List<object>? Enum { get; set; }
    public string? Title { get; set; }
    public bool? Deprecated { get; set; }
    public bool? ReadOnly { get; set; }
    public bool? WriteOnly { get; set; }
    public PropertyMetadata Clone()
    {
        return new PropertyMetadata
        {
            Type = Type,
            Description = Description,
            Format = Format,
            Example = Example,
            Default = Default,
            Minimum = Minimum,
            Maximum = Maximum,
            MinLength = MinLength,
            MaxLength = MaxLength,
            Pattern = Pattern,
            Enum = Enum?.ToList(),
            Title = Title,
            Deprecated = Deprecated,
            ReadOnly = ReadOnly,
            WriteOnly = WriteOnly
        };
    }
}