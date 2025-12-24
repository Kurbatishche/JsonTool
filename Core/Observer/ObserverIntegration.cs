using JsonTool.Core.Command;

namespace JsonTool.Core.Observer;
public class ObserverIntegration : IDisposable
{
    private readonly SchemaChangeNotifier _notifier;
    private readonly AutoSaveObserver _autoSaveObserver;
    private readonly HistoryObserver _historyObserver;
    private readonly UIUpdateObserver _uiUpdateObserver;
    private bool _disposed;
    public SchemaChangeNotifier Notifier => _notifier;
    public AutoSaveObserver AutoSave => _autoSaveObserver;
    public HistoryObserver History => _historyObserver;
    public UIUpdateObserver UIUpdate => _uiUpdateObserver;
    public ObserverIntegration(
        Func<Task> saveAction,
        TimeSpan? saveDelay = null,
        int maxHistorySize = 1000)
    {
        _notifier = new SchemaChangeNotifier();
        _autoSaveObserver = new AutoSaveObserver(saveAction, saveDelay);
        _historyObserver = new HistoryObserver(maxHistorySize);
        _uiUpdateObserver = new UIUpdateObserver();
        _notifier.Attach(_historyObserver);    // Пріоритет 10 - перший
        _notifier.Attach(_autoSaveObserver);   // Пріоритет 50 - середній
        _notifier.Attach(_uiUpdateObserver);   // Пріоритет 100 - останній
    }
    public void IntegrateWithCommandManager(SchemaCommandManager commandManager)
    {
        _notifier.IntegrateWithCommandManager(commandManager);
    }
    public void AddObserver(ISchemaObserver observer)
    {
        _notifier.Attach(observer);
    }
    public void RemoveObserver(string observerId)
    {
        _notifier.Detach(observerId);
    }
    public void NotifyChange(ChangeType changeType, string? propertyPath = null, object? oldValue = null, object? newValue = null)
    {
        _notifier.NotifySchemaChanged(new SchemaChangeNotification
        {
            ChangeType = changeType,
            PropertyPath = propertyPath,
            OldValue = oldValue,
            NewValue = newValue,
            IsModified = true
        });
    }
    public void NotifySaved(string filePath, bool success, string? errorMessage = null)
    {
        _notifier.NotifySchemaSaved(new SchemaSaveNotification
        {
            FilePath = filePath,
            Success = success,
            ErrorMessage = errorMessage
        });
    }
    public void NotifyLoaded(string filePath, bool success, int propertiesCount = 0, string? errorMessage = null)
    {
        _notifier.NotifySchemaLoaded(new SchemaLoadNotification
        {
            FilePath = filePath,
            Success = success,
            PropertiesCount = propertiesCount,
            ErrorMessage = errorMessage
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _autoSaveObserver.Dispose();
        _notifier.Dispose();
    }
}