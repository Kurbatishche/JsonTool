using Newtonsoft.Json.Linq;

namespace JsonTool.Core.Command;
public class DeletePropertyCommand : SchemaCommandBase
{
    private readonly JObject _schema;
    private readonly string _propertyPath;
    private JToken? _deletedValue;
    private int _originalIndex;
    private bool _wasInRequired;
    private string? _parentPropertiesPath;

    public override string Description => $"Delete property '{_propertyPath}'";
    public string PropertyPath => _propertyPath;
    public JToken? DeletedValue => _deletedValue;
    public DeletePropertyCommand(JObject schema, string propertyPath)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _propertyPath = propertyPath ?? throw new ArgumentNullException(nameof(propertyPath));
    }
    public static DeletePropertyCommand CreateForSchemaProperty(JObject schema, string propertyName)
    {
        return new DeletePropertyCommand(schema, $"properties.{propertyName}");
    }

    public override bool CanExecute()
    {
        if (!base.CanExecute()) return false;
        var token = _schema.SelectToken(_propertyPath);
        return token != null;
    }

    protected override void ExecuteCore()
    {
        var token = _schema.SelectToken(_propertyPath);
        _deletedValue = token?.DeepClone();
        _originalIndex = GetPropertyIndex();
        _wasInRequired = CheckAndRemoveFromRequired();
        _parentPropertiesPath = GetParentPropertiesPath();
        RemoveProperty();
    }

    protected override void UndoCore()
    {
        if (_deletedValue == null) return;
        RestoreProperty();
        if (_wasInRequired)
        {
            RestoreToRequired();
        }
    }

    private int GetPropertyIndex()
    {
        var parentPath = GetParentPath(_propertyPath);
        var propertyName = GetPropertyName(_propertyPath);

        JToken? parent = string.IsNullOrEmpty(parentPath)
            ? _schema
            : _schema.SelectToken(parentPath);

        if (parent is JObject parentObj)
        {
            int index = 0;
            foreach (var prop in parentObj.Properties())
            {
                if (prop.Name == propertyName)
                {
                    return index;
                }
                index++;
            }
        }

        return -1;
    }

    private bool CheckAndRemoveFromRequired()
    {
        if (!_propertyPath.StartsWith("properties."))
        {
            return false;
        }

        var propertyName = _propertyPath.Replace("properties.", "");
        if (propertyName.Contains('.'))
        {
            return false;
        }

        var required = _schema["required"] as JArray;
        if (required == null) return false;

        var item = required.FirstOrDefault(r => r.ToString() == propertyName);
        if (item != null)
        {
            required.Remove(item);
            return true;
        }

        return false;
    }

    private void RestoreToRequired()
    {
        var propertyName = _propertyPath.Replace("properties.", "");
        
        var required = _schema["required"] as JArray;
        if (required == null)
        {
            required = new JArray();
            _schema["required"] = required;
        }

        if (!required.Any(r => r.ToString() == propertyName))
        {
            required.Add(propertyName);
        }
    }

    private string? GetParentPropertiesPath()
    {
        var lastDot = _propertyPath.LastIndexOf('.');
        return lastDot > 0 ? _propertyPath.Substring(0, lastDot) : null;
    }

    private void RemoveProperty()
    {
        var parentPath = GetParentPath(_propertyPath);
        var propertyName = GetPropertyName(_propertyPath);

        JToken? parent = string.IsNullOrEmpty(parentPath)
            ? _schema
            : _schema.SelectToken(parentPath);

        if (parent is JObject parentObj)
        {
            parentObj.Remove(propertyName);
        }
    }

    private void RestoreProperty()
    {
        var parentPath = GetParentPath(_propertyPath);
        var propertyName = GetPropertyName(_propertyPath);

        JObject? parent;
        if (string.IsNullOrEmpty(parentPath))
        {
            parent = _schema;
        }
        else
        {
            var parentToken = _schema.SelectToken(parentPath);
            if (parentToken == null)
            {
                parent = EnsurePathExists(_schema, parentPath);
            }
            else
            {
                parent = parentToken as JObject;
            }
        }

        if (parent != null && _deletedValue != null)
        {
            InsertAtPosition(parent, propertyName, _deletedValue.DeepClone(), _originalIndex);
        }
    }

    private static void InsertAtPosition(JObject parent, string propertyName, JToken value, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= parent.Count)
        {
            parent[propertyName] = value;
            return;
        }
        var properties = parent.Properties().ToList();
        parent.RemoveAll();
        for (int i = 0; i < properties.Count; i++)
        {
            if (i == targetIndex)
            {
                parent[propertyName] = value;
            }
            parent[properties[i].Name] = properties[i].Value;
        }
        if (targetIndex >= properties.Count)
        {
            parent[propertyName] = value;
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
}