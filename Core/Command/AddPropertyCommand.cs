using Newtonsoft.Json.Linq;

namespace JsonTool.Core.Command;
public class AddPropertyCommand : SchemaCommandBase
{
    private readonly JObject _schema;
    private readonly string _parentPath;
    private readonly string _propertyName;
    private readonly JToken _propertyValue;
    private bool _parentCreated;
    private JToken? _previousParentState;

    public override string Description => $"Add property '{_propertyName}' to '{_parentPath}'";
    public string ParentPath => _parentPath;
    public string PropertyName => _propertyName;
    public JToken PropertyValue => _propertyValue;
    public string FullPath => string.IsNullOrEmpty(_parentPath) 
        ? _propertyName 
        : $"{_parentPath}.{_propertyName}";
    public AddPropertyCommand(JObject schema, string parentPath, string propertyName, JToken propertyValue)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _parentPath = parentPath ?? string.Empty;
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _propertyValue = propertyValue ?? throw new ArgumentNullException(nameof(propertyValue));
    }
    public static AddPropertyCommand CreateSchemaProperty(
        JObject schema, 
        string propertyName, 
        string type,
        string? description = null,
        object? defaultValue = null)
    {
        var propDefinition = new JObject
        {
            ["type"] = type
        };

        if (!string.IsNullOrEmpty(description))
        {
            propDefinition["description"] = description;
        }

        if (defaultValue != null)
        {
            propDefinition["default"] = JToken.FromObject(defaultValue);
        }

        return new AddPropertyCommand(schema, "properties", propertyName, propDefinition);
    }

    public override bool CanExecute()
    {
        if (!base.CanExecute()) return false;
        var fullPath = FullPath;
        var existing = _schema.SelectToken(fullPath);
        
        return existing == null;
    }

    protected override void ExecuteCore()
    {
        JObject parent;

        if (string.IsNullOrEmpty(_parentPath))
        {
            parent = _schema;
            _previousParentState = null;
        }
        else
        {
            var parentToken = _schema.SelectToken(_parentPath);
            
            if (parentToken == null)
            {
                _parentCreated = true;
                _previousParentState = null;
                parent = EnsurePathExists(_schema, _parentPath);
            }
            else if (parentToken is JObject parentObj)
            {
                _parentCreated = false;
                _previousParentState = parentObj.DeepClone();
                parent = parentObj;
            }
            else
            {
                throw new InvalidOperationException($"Parent path '{_parentPath}' is not an object");
            }
        }
        parent[_propertyName] = _propertyValue.DeepClone();
    }

    protected override void UndoCore()
    {
        if (_parentCreated)
        {
            RemoveCreatedPath(_schema, _parentPath);
        }
        else if (string.IsNullOrEmpty(_parentPath))
        {
            _schema.Remove(_propertyName);
        }
        else
        {
            var parentToken = _schema.SelectToken(_parentPath);
            if (parentToken is JObject parentObj)
            {
                parentObj.Remove(_propertyName);
            }
        }
    }

    private static JObject EnsurePathExists(JObject root, string path)
    {
        var parts = path.Split('.');
        JObject current = root;

        foreach (var part in parts)
        {
            var next = current[part];
            
            if (next == null)
            {
                var newObj = new JObject();
                current[part] = newObj;
                current = newObj;
            }
            else if (next is JObject nextObj)
            {
                current = nextObj;
            }
            else
            {
                throw new InvalidOperationException($"Path part '{part}' is not an object");
            }
        }

        return current;
    }

    private static void RemoveCreatedPath(JObject root, string path)
    {
        var parts = path.Split('.');
        if (parts.Length == 0) return;
        var firstPart = parts[0];
        root.Remove(firstPart);
    }
}