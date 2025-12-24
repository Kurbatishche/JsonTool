using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JsonTool.Core.Models;
public class JsonPropertyMetadata : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _example = string.Empty;
    private string _dataType = string.Empty;
    private string _format = string.Empty;
    private bool _isRequired;
    private string _path = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string Example
    {
        get => _example;
        set { _example = value; OnPropertyChanged(); }
    }

    public string DataType
    {
        get => _dataType;
        set { _dataType = value; OnPropertyChanged(); }
    }

    public string Format
    {
        get => _format;
        set { _format = value; OnPropertyChanged(); }
    }

    public bool IsRequired
    {
        get => _isRequired;
        set { _isRequired = value; OnPropertyChanged(); }
    }

    public string Path
    {
        get => _path;
        set { _path = value; OnPropertyChanged(); }
    }

    public List<JsonPropertyMetadata> Children { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}