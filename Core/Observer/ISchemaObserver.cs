using JsonTool.Core.Command;

namespace JsonTool.Core.Observer;
public interface ISchemaObserver
{
    string ObserverId { get; }
    int Priority { get; }
    bool IsEnabled { get; set; }
    void OnSchemaChanged(SchemaChangeNotification notification);
    void OnSchemaSaved(SchemaSaveNotification notification);
    void OnSchemaLoaded(SchemaLoadNotification notification);
    void OnCommandExecuted(CommandNotification notification);
}
public abstract class NotificationBase
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public string? Source { get; set; }
}
public class SchemaChangeNotification : NotificationBase
{
    public string SchemaPath { get; set; } = string.Empty;
    public string? PropertyPath { get; set; }
    public ChangeType ChangeType { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public bool IsModified { get; set; }
}
public class SchemaSaveNotification : NotificationBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
}
public class SchemaLoadNotification : NotificationBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PropertiesCount { get; set; }
}
public class CommandNotification : NotificationBase
{
    public ISchemaCommand Command { get; set; } = null!;
    public CommandActionType ActionType { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
public enum ChangeType
{
    PropertyAdded,
    PropertyUpdated,
    PropertyDeleted,
    MetadataUpdated,
    SchemaReloaded,
    ContentChanged
}
public enum CommandActionType
{
    Execute,
    Undo,
    Redo
}