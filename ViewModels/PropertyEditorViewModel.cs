using System.Windows.Input;
using JsonTool.Core.Command;
using JsonTool.Core.Models;

namespace JsonTool.ViewModels;
public class PropertyEditorViewModel : ViewModelBase
{
    private readonly CommandInvoker _commandInvoker;
    private readonly Action<JsonPropertyMetadata>? _onPropertyUpdated;
    private JsonPropertyMetadata? _property;

    private string _description = string.Empty;
    private string _example = string.Empty;
    private string _dataType = string.Empty;
    private string _format = string.Empty;

    public PropertyEditorViewModel(CommandInvoker commandInvoker, Action<JsonPropertyMetadata>? onPropertyUpdated = null)
    {
        _commandInvoker = commandInvoker;
        _onPropertyUpdated = onPropertyUpdated;

        SaveCommand = new RelayCommand(SaveChanges, () => Property != null && HasChanges);
        RevertCommand = new RelayCommand(RevertChanges, () => Property != null && HasChanges);

        DataTypes = new[]
        {
            "string", "number", "integer", "boolean", "object", "array", "null"
        };

        Formats = new[]
        {
            "", "date", "date-time", "time", "email", "uri", "uuid", "hostname", "ipv4", "ipv6"
        };
    }

    public JsonPropertyMetadata? Property
    {
        get => _property;
        set
        {
            if (SetProperty(ref _property, value))
            {
                LoadPropertyValues();
                OnPropertyChanged(nameof(HasProperty));
            }
        }
    }

    public bool HasProperty => Property != null;

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
            {
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Example
    {
        get => _example;
        set
        {
            if (SetProperty(ref _example, value))
            {
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string DataType
    {
        get => _dataType;
        set
        {
            if (SetProperty(ref _dataType, value))
            {
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string Format
    {
        get => _format;
        set
        {
            if (SetProperty(ref _format, value))
            {
                OnPropertyChanged(nameof(HasChanges));
            }
        }
    }

    public string[] DataTypes { get; }
    public string[] Formats { get; }

    public bool HasChanges
    {
        get
        {
            if (Property == null) return false;
            return Description != Property.Description ||
                   Example != Property.Example ||
                   DataType != Property.DataType ||
                   Format != Property.Format;
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand RevertCommand { get; }

    private void LoadPropertyValues()
    {
        if (Property == null)
        {
            Description = string.Empty;
            Example = string.Empty;
            DataType = string.Empty;
            Format = string.Empty;
        }
        else
        {
            Description = Property.Description;
            Example = Property.Example;
            DataType = Property.DataType;
            Format = Property.Format;
        }
    }

    private void SaveChanges()
    {
        if (Property == null) return;
        if (Description != Property.Description)
        {
            var cmd = new EditPropertyCommand(Property, nameof(Property.Description), 
                Property.Description, Description, _onPropertyUpdated);
            _commandInvoker.ExecuteCommand(cmd);
        }

        if (Example != Property.Example)
        {
            var cmd = new EditPropertyCommand(Property, nameof(Property.Example), 
                Property.Example, Example, _onPropertyUpdated);
            _commandInvoker.ExecuteCommand(cmd);
        }

        if (DataType != Property.DataType)
        {
            var cmd = new EditPropertyCommand(Property, nameof(Property.DataType), 
                Property.DataType, DataType, _onPropertyUpdated);
            _commandInvoker.ExecuteCommand(cmd);
        }

        if (Format != Property.Format)
        {
            var cmd = new EditPropertyCommand(Property, nameof(Property.Format), 
                Property.Format, Format, _onPropertyUpdated);
            _commandInvoker.ExecuteCommand(cmd);
        }

        OnPropertyChanged(nameof(HasChanges));
    }

    private void RevertChanges()
    {
        LoadPropertyValues();
        OnPropertyChanged(nameof(HasChanges));
    }
}