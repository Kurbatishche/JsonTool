using JsonTool.Core.Command;

namespace JsonTool.Core.Observer;
public class SchemaChangeNotifier : ISchemaSubject, IDisposable
{
    private readonly List<ISchemaObserver> _observers = new();
    private readonly object _lock = new();
    private bool _notificationsSuspended;
    private readonly Queue<Action> _pendingNotifications = new();
    private bool _disposed;
    public event EventHandler<SchemaChangeNotification>? SchemaChanged;
    public event EventHandler<SchemaSaveNotification>? SchemaSaved;
    public event EventHandler<SchemaLoadNotification>? SchemaLoaded;
    public event EventHandler<CommandNotification>? CommandExecuted;
    public int ObserverCount
    {
        get
        {
            lock (_lock)
            {
                return _observers.Count;
            }
        }
    }
    public bool NotificationsSuspended => _notificationsSuspended;

    public void Attach(ISchemaObserver observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));

        lock (_lock)
        {
            if (_observers.Any(o => o.ObserverId == observer.ObserverId))
            {
                throw new InvalidOperationException($"Observer with ID '{observer.ObserverId}' is already attached");
            }

            _observers.Add(observer);
            _observers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }
    }

    public void Detach(ISchemaObserver observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        Detach(observer.ObserverId);
    }

    public void Detach(string observerId)
    {
        lock (_lock)
        {
            var observer = _observers.FirstOrDefault(o => o.ObserverId == observerId);
            if (observer != null)
            {
                _observers.Remove(observer);
            }
        }
    }

    public void NotifySchemaChanged(SchemaChangeNotification notification)
    {
        if (_notificationsSuspended)
        {
            lock (_lock)
            {
                _pendingNotifications.Enqueue(() => NotifySchemaChanged(notification));
            }
            return;
        }
        SchemaChanged?.Invoke(this, notification);
        NotifyObservers(o => o.OnSchemaChanged(notification));
    }

    public void NotifySchemaSaved(SchemaSaveNotification notification)
    {
        if (_notificationsSuspended)
        {
            lock (_lock)
            {
                _pendingNotifications.Enqueue(() => NotifySchemaSaved(notification));
            }
            return;
        }

        SchemaSaved?.Invoke(this, notification);
        NotifyObservers(o => o.OnSchemaSaved(notification));
    }

    public void NotifySchemaLoaded(SchemaLoadNotification notification)
    {
        if (_notificationsSuspended)
        {
            lock (_lock)
            {
                _pendingNotifications.Enqueue(() => NotifySchemaLoaded(notification));
            }
            return;
        }

        SchemaLoaded?.Invoke(this, notification);
        NotifyObservers(o => o.OnSchemaLoaded(notification));
    }

    public void NotifyCommandExecuted(CommandNotification notification)
    {
        if (_notificationsSuspended)
        {
            lock (_lock)
            {
                _pendingNotifications.Enqueue(() => NotifyCommandExecuted(notification));
            }
            return;
        }

        CommandExecuted?.Invoke(this, notification);
        NotifyObservers(o => o.OnCommandExecuted(notification));
    }

    public IReadOnlyList<ISchemaObserver> GetObservers()
    {
        lock (_lock)
        {
            return _observers.ToList().AsReadOnly();
        }
    }

    public bool IsAttached(string observerId)
    {
        lock (_lock)
        {
            return _observers.Any(o => o.ObserverId == observerId);
        }
    }

    public void SuspendNotifications()
    {
        _notificationsSuspended = true;
    }

    public void ResumeNotifications()
    {
        _notificationsSuspended = false;
        Queue<Action> pending;
        lock (_lock)
        {
            pending = new Queue<Action>(_pendingNotifications);
            _pendingNotifications.Clear();
        }

        while (pending.Count > 0)
        {
            var action = pending.Dequeue();
            action();
        }
    }
    public void IntegrateWithCommandManager(SchemaCommandManager commandManager)
    {
        commandManager.CommandExecuted += (s, e) =>
        {
            NotifyCommandExecuted(new CommandNotification
            {
                Command = e.Command,
                ActionType = CommandActionType.Execute,
                Success = true,
                Source = "CommandManager"
            });
            NotifySchemaChanged(new SchemaChangeNotification
            {
                ChangeType = GetChangeTypeFromCommand(e.Command),
                IsModified = true,
                Source = e.Command.Description
            });
        };

        commandManager.CommandUndone += (s, e) =>
        {
            NotifyCommandExecuted(new CommandNotification
            {
                Command = e.Command,
                ActionType = CommandActionType.Undo,
                Success = true,
                Source = "CommandManager"
            });

            NotifySchemaChanged(new SchemaChangeNotification
            {
                ChangeType = ChangeType.ContentChanged,
                IsModified = true,
                Source = $"Undo: {e.Command.Description}"
            });
        };

        commandManager.CommandRedone += (s, e) =>
        {
            NotifyCommandExecuted(new CommandNotification
            {
                Command = e.Command,
                ActionType = CommandActionType.Redo,
                Success = true,
                Source = "CommandManager"
            });

            NotifySchemaChanged(new SchemaChangeNotification
            {
                ChangeType = ChangeType.ContentChanged,
                IsModified = true,
                Source = $"Redo: {e.Command.Description}"
            });
        };
    }

    private ChangeType GetChangeTypeFromCommand(ISchemaCommand command)
    {
        return command switch
        {
            AddPropertyCommand => ChangeType.PropertyAdded,
            DeletePropertyCommand => ChangeType.PropertyDeleted,
            UpdatePropertyCommand => ChangeType.PropertyUpdated,
            UpdateMetadataCommand => ChangeType.MetadataUpdated,
            _ => ChangeType.ContentChanged
        };
    }

    private void NotifyObservers(Action<ISchemaObserver> action)
    {
        List<ISchemaObserver> observers;
        lock (_lock)
        {
            observers = _observers.Where(o => o.IsEnabled).ToList();
        }

        foreach (var observer in observers)
        {
            try
            {
                action(observer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Observer {observer.ObserverId} error: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var observer in _observers.OfType<IDisposable>())
            {
                observer.Dispose();
            }
            _observers.Clear();
            _pendingNotifications.Clear();
        }
    }
}