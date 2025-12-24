using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JsonTool.Core.Models;
public class SchemaProperty : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _type = "string";
    private string? _format;
    private string? _pattern;
    private string? _description;
    private string? _example;
    private object? _defaultValue;
    private bool _isRequired;
    private bool _isNullable;
    private decimal? _minimum;
    private decimal? _maximum;
    private int? _minLength;
    private int? _maxLength;
    private int? _minItems;
    private int? _maxItems;
    private bool _isExpanded = true;
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }
    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }
    public string? Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }
    public string? Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }
    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }
    public string? Example
    {
        get => _example;
        set => SetProperty(ref _example, value);
    }
    public object? DefaultValue
    {
        get => _defaultValue;
        set => SetProperty(ref _defaultValue, value);
    }
    public bool IsRequired
    {
        get => _isRequired;
        set => SetProperty(ref _isRequired, value);
    }
    public bool IsNullable
    {
        get => _isNullable;
        set => SetProperty(ref _isNullable, value);
    }
    public decimal? Minimum
    {
        get => _minimum;
        set => SetProperty(ref _minimum, value);
    }
    public decimal? Maximum
    {
        get => _maximum;
        set => SetProperty(ref _maximum, value);
    }
    public int? MinLength
    {
        get => _minLength;
        set => SetProperty(ref _minLength, value);
    }
    public int? MaxLength
    {
        get => _maxLength;
        set => SetProperty(ref _maxLength, value);
    }
    public int? MinItems
    {
        get => _minItems;
        set => SetProperty(ref _minItems, value);
    }
    public int? MaxItems
    {
        get => _maxItems;
        set => SetProperty(ref _maxItems, value);
    }
    public ObservableCollection<string> EnumValues { get; } = new();
    public ObservableCollection<SchemaProperty> Children { get; } = new();
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public int Depth { get; set; }
    public SchemaProperty? Parent { get; set; }
    public string DisplayType => string.IsNullOrEmpty(Format) ? Type : $"{Type} ({Format})";
    public string FullPath => string.IsNullOrEmpty(Path) ? Name : $"{Path}.{Name}";
    public bool HasChildren => Children.Count > 0;
    public string TypeIcon => Type switch
    {
        "string" => "📝",
        "integer" => "🔢",
        "number" => "🔢",
        "boolean" => "✓",
        "object" => "📦",
        "array" => "📋",
        "null" => "∅",
        _ => "❓"
    };

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public override string ToString()
    {
        var required = IsRequired ? " *" : "";
        return $"{Name}: {DisplayType}{required}";
    }
    public SchemaProperty Clone()
    {
        var clone = new SchemaProperty
        {
            Name = Name,
            Path = Path,
            Type = Type,
            Format = Format,
            Pattern = Pattern,
            Description = Description,
            Example = Example,
            DefaultValue = DefaultValue,
            IsRequired = IsRequired,
            IsNullable = IsNullable,
            Minimum = Minimum,
            Maximum = Maximum,
            MinLength = MinLength,
            MaxLength = MaxLength,
            MinItems = MinItems,
            MaxItems = MaxItems,
            Depth = Depth
        };

        foreach (var enumValue in EnumValues)
        {
            clone.EnumValues.Add(enumValue);
        }

        foreach (var child in Children)
        {
            var childClone = child.Clone();
            childClone.Parent = clone;
            clone.Children.Add(childClone);
        }

        return clone;
    }
}