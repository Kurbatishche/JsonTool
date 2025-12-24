using JsonTool.Core.Models;

namespace JsonTool.Core.Command;
public class EditPropertyCommand : SchemaCommandBase
{
    private readonly JsonPropertyMetadata _property;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;
    private readonly Action<JsonPropertyMetadata>? _onPropertyChanged;

    public override string Description => $"Edit {_property.Name}.{_propertyName}";

    public EditPropertyCommand(
        JsonPropertyMetadata property,
        string propertyName,
        object? oldValue,
        object? newValue,
        Action<JsonPropertyMetadata>? onPropertyChanged = null)
    {
        _property = property;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
        _onPropertyChanged = onPropertyChanged;
    }

    public override bool CanExecute()
    {
        return !Equals(_oldValue, _newValue) && !IsExecuted;
    }

    protected override void ExecuteCore()
    {
        SetPropertyValue(_newValue);
        _onPropertyChanged?.Invoke(_property);
    }

    protected override void UndoCore()
    {
        SetPropertyValue(_oldValue);
        _onPropertyChanged?.Invoke(_property);
    }

    private void SetPropertyValue(object? value)
    {
        var propInfo = typeof(JsonPropertyMetadata).GetProperty(_propertyName);
        propInfo?.SetValue(_property, value);
    }
}