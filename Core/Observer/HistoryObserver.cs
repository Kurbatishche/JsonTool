namespace JsonTool.Core.Observer;
public class HistoryObserver : SchemaObserverBase
{
    private readonly List<HistoryEntry> _history = new();
    private readonly object _lock = new();
    private readonly int _maxHistorySize;

    public override string ObserverId => "HistoryObserver";
    public override int Priority => 10; // Високий пріоритет - записуємо першими
    public int MaxHistorySize => _maxHistorySize;
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _history.Count;
            }
        }
    }
    public event EventHandler<HistoryEntryAddedEventArgs>? EntryAdded;
    public HistoryObserver(int maxHistorySize = 1000)
    {
        _maxHistorySize = maxHistorySize;
    }

    public override void OnSchemaChanged(SchemaChangeNotification notification)
    {
        AddEntry(new HistoryEntry
        {
            EntryType = HistoryEntryType.SchemaChanged,
            Timestamp = notification.Timestamp,
            Description = GetChangeDescription(notification),
            ChangeType = notification.ChangeType,
            PropertyPath = notification.PropertyPath,
            OldValue = notification.OldValue?.ToString(),
            NewValue = notification.NewValue?.ToString(),
            Source = notification.Source
        });
    }

    public override void OnSchemaSaved(SchemaSaveNotification notification)
    {
        AddEntry(new HistoryEntry
        {
            EntryType = HistoryEntryType.SchemaSaved,
            Timestamp = notification.Timestamp,
            Description = notification.Success 
                ? $"Schema saved to {notification.FilePath}" 
                : $"Failed to save schema: {notification.ErrorMessage}",
            FilePath = notification.FilePath,
            Success = notification.Success,
            Source = notification.Source
        });
    }

    public override void OnSchemaLoaded(SchemaLoadNotification notification)
    {
        AddEntry(new HistoryEntry
        {
            EntryType = HistoryEntryType.SchemaLoaded,
            Timestamp = notification.Timestamp,
            Description = notification.Success 
                ? $"Schema loaded from {notification.FilePath} ({notification.PropertiesCount} properties)" 
                : $"Failed to load schema: {notification.ErrorMessage}",
            FilePath = notification.FilePath,
            Success = notification.Success,
            Source = notification.Source
        });
    }

    public override void OnCommandExecuted(CommandNotification notification)
    {
        var actionText = notification.ActionType switch
        {
            CommandActionType.Execute => "Executed",
            CommandActionType.Undo => "Undone",
            CommandActionType.Redo => "Redone",
            _ => "Unknown"
        };

        AddEntry(new HistoryEntry
        {
            EntryType = HistoryEntryType.CommandExecuted,
            Timestamp = notification.Timestamp,
            Description = $"{actionText}: {notification.Command.Description}",
            CommandId = notification.Command.Id,
            CommandAction = notification.ActionType,
            Success = notification.Success,
            Source = notification.Source
        });
    }
    public IReadOnlyList<HistoryEntry> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList().AsReadOnly();
        }
    }
    public IReadOnlyList<HistoryEntry> GetRecentHistory(int count)
    {
        lock (_lock)
        {
            return _history.TakeLast(count).ToList().AsReadOnly();
        }
    }
    public IReadOnlyList<HistoryEntry> GetHistoryByType(HistoryEntryType type)
    {
        lock (_lock)
        {
            return _history.Where(e => e.EntryType == type).ToList().AsReadOnly();
        }
    }
    public IReadOnlyList<HistoryEntry> GetHistoryByPeriod(DateTime from, DateTime to)
    {
        lock (_lock)
        {
            return _history
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .ToList()
                .AsReadOnly();
        }
    }
    public void Clear()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }
    public string ExportToText()
    {
        lock (_lock)
        {
            var lines = _history.Select(e => 
                $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.EntryType}] {e.Description}");
            return string.Join(Environment.NewLine, lines);
        }
    }

    private void AddEntry(HistoryEntry entry)
    {
        lock (_lock)
        {
            _history.Add(entry);
            while (_history.Count > _maxHistorySize)
            {
                _history.RemoveAt(0);
            }
        }

        EntryAdded?.Invoke(this, new HistoryEntryAddedEventArgs { Entry = entry });
    }

    private static string GetChangeDescription(SchemaChangeNotification notification)
    {
        var path = notification.PropertyPath ?? "schema";
        return notification.ChangeType switch
        {
            ChangeType.PropertyAdded => $"Property added: {path}",
            ChangeType.PropertyUpdated => $"Property updated: {path}",
            ChangeType.PropertyDeleted => $"Property deleted: {path}",
            ChangeType.MetadataUpdated => $"Metadata updated: {path}",
            ChangeType.SchemaReloaded => "Schema reloaded",
            ChangeType.ContentChanged => $"Content changed: {notification.Source ?? path}",
            _ => $"Change: {path}"
        };
    }
}
public class HistoryEntry
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; }
    public HistoryEntryType EntryType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? PropertyPath { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? FilePath { get; set; }
    public ChangeType? ChangeType { get; set; }
    public Guid? CommandId { get; set; }
    public CommandActionType? CommandAction { get; set; }
    public bool Success { get; set; } = true;
    public string? Source { get; set; }
}
public enum HistoryEntryType
{
    SchemaChanged,
    SchemaSaved,
    SchemaLoaded,
    CommandExecuted
}
public class HistoryEntryAddedEventArgs : EventArgs
{
    public HistoryEntry Entry { get; set; } = null!;
}