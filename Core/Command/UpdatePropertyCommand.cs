using Newtonsoft.Json.Linq;

namespace JsonTool.Core.Command;
public class UpdatePropertyCommand : SchemaCommandBase
{
    private readonly JObject _schema;
    private readonly string _propertyPath;
    private readonly JToken _newValue;
    private JToken? _previousValue;
    private bool _propertyExisted;

    public override string Description => $"Update property '{_propertyPath}'";
    public string PropertyPath => _propertyPath;
    public JToken NewValue => _newValue;
    public JToken? PreviousValue => _previousValue;
    public UpdatePropertyCommand(JObject schema, string propertyPath, JToken newValue)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
        _newValue = newValue ?? throw new ArgumentNullException(nameof(newValue));
    }

    public override bool CanExecute()
    {
        if (!base.CanExecute()) return false;
        var parentPath = GetParentPath(_propertyPath);
        if (string.IsNullOrEmpty(parentPath))
        {
            return true; // Кореневий рівень
        }

        var parent = _schema.SelectToken(parentPath);
        return parent != null;
    }

    protected override void ExecuteCore()
    {
        var existingToken = _schema.SelectToken(_propertyPath);
        _propertyExisted = existingToken != null;
        _previousValue = existingToken?.DeepClone();
        SetValueAtPath(_schema, _propertyPath, _newValue.DeepClone());
    }

    protected override void UndoCore()
    {
        if (_propertyExisted && _previousValue != null)
        {
            SetValueAtPath(_schema, _propertyPath, _previousValue.DeepClone());
        }
        else
        {
            RemoveAtPath(_schema, _propertyPath);
        }
    }

    private static string GetParentPath(string path)
    {
        var lastDot = path.LastIndexOf('.');
        return lastDot > 0 ? path.Substring(0, lastDot) : string.Empty;
    }

    private static string GetPropertyName(string path)
    {
        var lastDot = path.LastIndexOf('.');
        return lastDot > 0 ? path.Substring(lastDot + 1) : path;
    }

    private static void SetValueAtPath(JObject root, string path, JToken value)
    {
        var parts = path.Split('.');
        JToken current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var next = current[part];
            
            if (next == null)
            {
                var newObj = new JObject();
                ((JObject)current)[part] = newObj;
                current = newObj;
            }
            else
            {
                current = next;
            }
        }

        var propertyName = parts[^1];
        if (current is JObject obj)
        {
            obj[propertyName] = value;
        }
    }

    private static void RemoveAtPath(JObject root, string path)
    {
        var parentPath = GetParentPath(path);
        var propertyName = GetPropertyName(path);

        JToken? parent = string.IsNullOrEmpty(parentPath) 
            ? root 
            : root.SelectToken(parentPath);

        if (parent is JObject parentObj)
        {
            parentObj.Remove(propertyName);
        }
    }
}